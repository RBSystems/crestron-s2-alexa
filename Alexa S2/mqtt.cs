using System;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text;
using Newtonsoft.Json;


namespace MQTT
{
    [Serializable]
    public class MQTTMessage
	{
		public string cmd { get; set; }
		public string parameters { get; set; }

        public MQTTMessage()
        {
            this.cmd = "";
            this.parameters = "";
        }
        
        public MQTTMessage(string command, string parameters)
        {
            this.cmd = command;
            this.parameters = parameters;
        }

        public string toJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public void fromJson(string json)
        {
            MQTTMessage msg = JsonConvert.DeserializeObject<MQTTMessage>(json);
            this.cmd = msg.cmd;
            this.parameters = msg.parameters;
        }
	}

    public class MQTTClient
    {
        public Action<string> MQTTDataHandler {get; set; }

        public void Connect()
        {
            string iotEndpoint = "a2dv8i6hr80q6l-ats.iot.us-east-1.amazonaws.com";
            int brokerPort = 8883;
            string clientId = Guid.NewGuid().ToString();

            Console.WriteLine("AWS IoT dotnet message consumer starting..");

            var caCert = X509Certificate.CreateFromCertFile(Path.Combine(Directory.GetCurrentDirectory(), "certs", "AmazonRootCA1.crt"));
            var clientCert = new X509Certificate2(Path.Combine(Directory.GetCurrentDirectory(), "certs", "bf1d861d1b-certificate.pem.crt.pfx"), "crestron");
            
            var client = new MqttClient(iotEndpoint, brokerPort, true, caCert, clientCert, MqttSslProtocols.TLSv1_2);
            client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            client.MqttMsgSubscribed += Client_MqttMsgSubscribed;

            client.Connect(clientId);
            Console.WriteLine($"Connected to AWS IoT with client ID: {clientId}");

            client.Subscribe(new string[] { "crestron" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
        }

        private static void Client_MqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e)
        {
            Console.WriteLine($"Successfully subscribed to the AWS IoT topic.");
        }

        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            Console.WriteLine("Message received: " + Encoding.UTF8.GetString(e.Message));
            MQTTDataHandler(Encoding.UTF8.GetString(e.Message));
        }

    }

}
