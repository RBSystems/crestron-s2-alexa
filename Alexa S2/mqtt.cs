
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using System.Threading.Tasks;
using System;
using System.Text;  

public class MQTTClient
{

    private static IManagedMqttClient client;

    public Action<string, string> ProcessMQTTMessage { get; set; }

    public async Task ConnectAsync(string mqttURI, int mqttPort, string mqttUser = "", string mqttPassword = "", bool mqttSecure = false)
    {
        string clientId = Guid.NewGuid().ToString();

        var messageBuilder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            //.WithCredentials(mqttUser, mqttPassword)
            .WithTcpServer(mqttURI, mqttPort)
            .WithCleanSession();

        var options = mqttSecure
            ? messageBuilder
            .WithTls()
            .Build()
            : messageBuilder
            .Build();

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(options)
            .Build();

        client = new MqttFactory().CreateManagedMqttClient();

        client.UseConnectedHandler(e =>
        {
            Console.WriteLine("Connected successfully with MQTT Brokers. {0}", e.AuthenticateResult);
        });
        client.UseDisconnectedHandler(e =>
        {
            Console.WriteLine("Disconnected from MQTT Brokers. {0}", e.Exception);
        });

        await client.StartAsync(managedOptions);

        client.UseApplicationMessageReceivedHandler(e =>
        {
            try
            {
                string topic = e.ApplicationMessage.Topic;
                if (string.IsNullOrWhiteSpace(topic) == false)
                {
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    Console.WriteLine($"Topic: {topic}. Message Received: {payload}");

                    // Pass the topic and payload back to the parent program for processing
                    ProcessMQTTMessage(topic, payload);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, ex);
            }
        });
    }

    public async Task SubscribeAsync(string topic, int qos = 1) =>
        await client.SubscribeAsync(new TopicFilterBuilder()
            .WithTopic(topic)
            .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos)
            .Build());

}