using System;
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

		static AsynchronousClient crestron = new AsynchronousClient();
		static MQTTClient mqtt = new MQTTClient();

		private static void Main(string[] args)
		{
			// Connect to MQTT (AWS Message Bus)
			Console.WriteLine("Connecting to MQTT Server...");
			// Add a mqtt message handler
			mqtt.MQTTDataHandler = new Action<string>(MQTTDataHandler);
			// connect
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

		private static void MQTTDataHandler(string msgJson)
		{
			MQTTMessage msg = new MQTTMessage();
			msg.fromJson(msgJson);

			Console.WriteLine("MQTT Message : {0}", msg);

			if (msg.cmd == "heartbeat")
			{
				crestron.heartbeatRequest();
			}
			else if (msg.cmd == "volume_up")
			{
				volumeUp();
			}
			else if (msg.cmd == "volume_down")
			{
				volumeDown();
			}
			else if (msg.cmd == "volume_set")
			{
				volumeSet(Int32.Parse(msg.parameters));
			}
		}

		private static void volumeSet(int volume_level)
		{
			// Send a connection request
			Console.WriteLine("Sending Volume Set: {0} ...", volume_level);
			// Add logic to increase/decrease volume to desired level
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
