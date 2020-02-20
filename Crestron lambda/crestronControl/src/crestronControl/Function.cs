using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using AlexaAPI;
using AlexaAPI.Request;
using AlexaAPI.Response;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using MQTT;

/*
crestronControl> dotnet lambda package -c Release -o ../CrestronLambda.zip -f netcoreapp2.1
*/

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace crestronControl
{
    public class Function
    {
        private SkillResponse response = null;
        private ILambdaContext context = null;
        const string LOCALENAME = "locale";
        const string USA_Locale = "en-US";
        static MQTTClient mqtt = new MQTTClient();


        /// <summary>
        /// Application entry point
        /// </summary>
        /// <param name="input"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public SkillResponse FunctionHandler(SkillRequest input, ILambdaContext ctx)
        {
            context = ctx;
            try
            {
                response = new SkillResponse();
                response.Response = new ResponseBody();
                response.Response.ShouldEndSession = false;
                response.Version = AlexaConstants.AlexaVersion;

                if (input.Request.Type.Equals(AlexaConstants.LaunchRequest))
                {
                    string locale = input.Request.Locale;
                    if (string.IsNullOrEmpty(locale))
                    {
                        locale = USA_Locale;
                    }

                    response.SessionAttributes = new Dictionary<string, object>() {{LOCALENAME, locale}};
                }
                else
                {
                    if (input.Request.Type.Equals(AlexaConstants.IntentRequest))
                    {
                       string locale = string.Empty;
                       Dictionary <string, object> dictionary = input.Session.Attributes;
                       if (dictionary != null)
                       {
                           if (dictionary.ContainsKey(LOCALENAME))
                           {
                               locale = (string) dictionary[LOCALENAME];
                           }
                       }
               
                       if (string.IsNullOrEmpty(locale))
                       {
                            locale = input.Request.Locale;
                       }

                       if (string.IsNullOrEmpty(locale))
                       {
                            locale = USA_Locale; 
                       }

                       response.SessionAttributes = new Dictionary<string, object>() {{LOCALENAME, locale}};
                       response.Response.OutputSpeech = ProcessIntentRequest(input);
                    }
                }
                Log(JsonConvert.SerializeObject(response));
                return response;
            }
            catch (Exception ex)
            {
                Log($"error :" + ex.Message);
            }
            return null; 
        }

        /// <summary>
        ///  prepare text for Ssml display
        /// </summary>
        /// <param name="speech"></param>
        /// <returns>string</returns>
        private string SsmlDecorate(string speech)
        {
            return @"<speak>" + speech + "</speak>";
        }

        /// <summary>
        /// Process all not dialog based Intents
        /// </summary>
        /// <param name="input"></param>
        /// <returns>IOutputSpeech innerResponse</returns>
        private IOutputSpeech ProcessIntentRequest(SkillRequest input)
        {
            var intentRequest = input.Request;
            MQTTMessage msg;

            IOutputSpeech innerResponse = new PlainTextOutputSpeech();

            mqtt.Connect();

            switch (intentRequest.Intent.Name)
            {
                case "VolumeUpIntent":
                    msg = new MQTTMessage("volume_up");
                    mqtt.Publish(msg.toJson());
                    Console.WriteLine($"Published: {0}", msg.toJson());

                    //innerResponse = new SsmlOutputSpeech();
                    //(innerResponse as SsmlOutputSpeech).Ssml = "Volume Up";
                    (innerResponse as PlainTextOutputSpeech).Text = "Crestron Volume Increased";
                    response.Response.ShouldEndSession = true;
                    break;
                case "VolumeDownIntent":
                    msg = new MQTTMessage("volume_down");
                    mqtt.Publish(msg.toJson());
                    Console.WriteLine($"Published: {0}", msg.toJson());

                    //innerResponse = new SsmlOutputSpeech();
                    //(innerResponse as SsmlOutputSpeech).Ssml = "Volume Down";
                    (innerResponse as PlainTextOutputSpeech).Text = "Crestron Volume Decreased";
                    response.Response.ShouldEndSession = true;
                    break;
                case "VolumeSetIntent":
                    int volLevel = Int32.Parse(intentRequest.Intent.Slots["VolumeLevel"].Value);
                    int crestronVolLevel = mapVolumeRange(volLevel);

                    msg = new MQTTMessage("volume_set", crestronVolLevel.ToString());

                    mqtt.Publish(msg.toJson());
                    Console.WriteLine($"Published: {0}", msg.toJson());

                    (innerResponse as PlainTextOutputSpeech).Text = string.Format("I set the volume to {0}", crestronVolLevel);
                    response.Response.ShouldEndSession = true;
                    break;
                case AlexaConstants.CancelIntent:
                    (innerResponse as PlainTextOutputSpeech).Text = "Goodbye!";
                    response.Response.ShouldEndSession = true;
                    break;

                case AlexaConstants.StopIntent:
                    (innerResponse as PlainTextOutputSpeech).Text = "Goodbye!";
                    response.Response.ShouldEndSession = true;                    
                    break;

                case AlexaConstants.HelpIntent:
                    (innerResponse as PlainTextOutputSpeech).Text = "You can say increase or decrease the volume."; 
                    break;

                default:
                    innerResponse = new SsmlOutputSpeech();
                    (innerResponse as SsmlOutputSpeech).Ssml =  @"<voice name=""Hans""><amazon:emotion name=""disappointed"" intensity=""high"">Shyzuh! I don't understand! What do you want from me?</amazon:emotion></voice>";
                    break;
            }
            if (innerResponse.Type == AlexaConstants.SSMLSpeech)
            {
                BuildCard("Crestron Switch", (innerResponse as SsmlOutputSpeech).Ssml);
                (innerResponse as SsmlOutputSpeech).Ssml = SsmlDecorate((innerResponse as SsmlOutputSpeech).Ssml);
            }  
            return innerResponse;
        }

        /// <summary>
        /// Build a simple card, setting its title and content field 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <returns>void</returns>
        private void BuildCard(string title, string output)
        {
            if (!string.IsNullOrEmpty(output))
            {                
                output = Regex.Replace(output, @"<.*?>", "");
                response.Response.Card = new SimpleCard()
                {
                    Title = title,
                    Content = output,
                };  
            }
        }

        /// <summary>
        /// logger interface
        /// </summary>
        /// <param name="text"></param>
        /// <returns>void</returns>
        private void Log(string text)
        {
            if (context != null)
            {
                context.Logger.LogLine(text);
            }
        }

        private int mapVolumeRange(int value)
        {
            return (((value - 1) * (65535 - 1)) / (10 - 1)) + 1;
        }

    }
}
