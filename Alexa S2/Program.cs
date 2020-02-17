using System;
using System.Threading.Tasks;
using Crestron;
using MQTT;


namespace Alexa_S2
{

	public enum CrestronButtonID
	{
		volume_up = 6,
		volume_down = 7
	};

	class Program
	{
		// Crestron configuration
		public const string server = "192.168.7.78";
		public const int port = 41790;
		public const int passcode = 1234;

		// MQTT Configuration
		const string mqttURI = "broker.hivemq.com";
        const int mqttPort = 1883;
		const string mqttTopic = "264b17c7-3fde-46bc-b46f-8af76ee6d445";

		static AsynchronousClient crestron = new AsynchronousClient();
		static MQTTClient mqtt = new MQTTClient();

		private static async Task Main(string[] args)
		{
			// Connect to MQTT (AWS Message Bus)
			Console.WriteLine("Connecting to MQTT Server...");
			// Add a mqtt message handler
			mqtt.MQTTDataHandler = new Action<string>(MQTTDataHandler);
			// connect
			//await mqtt.ConnectAsync(mqttURI, mqttPort);
			// subscribe to a topic
			//await mqtt.SubscribeAsync(mqttTopic);
			mqtt.Connect();

						
			// Connect to Crestron Processor
			Console.WriteLine("Connecting to Crestron Processor...");
			// Add a crestron message handler
			crestron.CrestronDataHandler = new Action<string>(CrestronDataHandler);
			// connect
			crestron.StartClient(server, port, passcode);
		}

		private static void CrestronDataHandler(string message)
		{
			Console.WriteLine("Crestron Data Received : {0}", message);
			// Do something with the data here
		}

		private static void MQTTDataHandler(string message)
		{
			Console.WriteLine("MQTT Message :");
			//Console.WriteLine("{0} : {1}", topic, payload);

			//MQTTMessage msg = JsonSerializer.Deserialize<MQTTMessage>(payload);
			//sendCrestronData(msg.data_type, msg.data_id, msg.value);

			if (message == "heartbeat")
			{
				crestron.heartbeatRequest();
			}
			else if (message == "volume_up")
			{
				volumeUp();
			}
			else if (message == "volume_down")
			{
				volumeDown();
			}
		}

		private static void volumeUp()
		{
			// Send a connection request
			Console.WriteLine("Sending Volume Up...");
			crestron.buttonPress((int)CrestronButtonID.volume_up);
		}

		private static void volumeDown()
		{
			// Send a connection request
			Console.WriteLine("Sending Volume Down...");
			crestron.buttonPress((int)CrestronButtonID.volume_down);
		}
	}
}
