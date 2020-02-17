using System;
using System.Xml;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Alexa_S2
{
	public enum CrestronDataType
	{
		digital,
		analog,
		serial
	};

	public enum CrestronButtonID
	{
		volume_up = 10,
		volume_down = 11
	};

	public class MQTTMessage
	{
		public CrestronDataType data_type { get; set; }
		public CrestronButtonID data_id { get; set; }
		public string value {get; set;}
	}

	class Program
	{
		public const string server = "192.168.7.102";
		public const int port = 41790;
		public const int passcode = 1234;
		static AsynchronousClient crestronClient = new AsynchronousClient();
		static MQTTClient mqtt = new MQTTClient();
		const string mqttURI = "test.mosquitto.org";
        const int mqttPort = 1883;
		const string mqttTopic = "18b9e2cd-8a6a-4864-a76b-957d9431d0f1";

		public static bool running = false;

		static async Task Main(string[] args)
		{
			// Connect to MQTT (AWS Message Bus)
			Console.WriteLine("Connecting to MQTT Server...");
			// Add a mqtt message handler
			mqtt.ProcessMQTTMessage = new Action<string, string>(ProcessMQTTMessage);
			// connect
			await mqtt.ConnectAsync(mqttURI, mqttPort);
			// subscribe to a topic
			await mqtt.SubscribeAsync(mqttTopic);

						
			// Connect to Crestron Processor
			Console.WriteLine("Connecting to Crestron Processor...");
			// Add a crestron message handler
			crestronClient.ProcessResponse = new Action<string>(ProcessCrestronMessage);
			// connect
			crestronClient.StartClient(server, port);
		}

		static void StartHeartbeats()
		{
			while (running)
			{
				Thread.Sleep(10000);
				heartbeatRequest();
			}
		}

		static void ProcessMQTTMessage(string topic, string payload)
		{
			Console.WriteLine("MQTT Message :");
			Console.WriteLine("{0} : {1}", topic, payload);

			if (payload == "heartbeat")
			{
				heartbeatRequest();
			}
			else if (payload == "volume_up")
			{
				volumeUp();
			}
			else if (payload == "volume_down")
			{
				volumeDown();
			}

			//MQTTMessage msg = JsonSerializer.Deserialize<MQTTMessage>(payload);
			//sendCrestronData(msg.data_type, msg.data_id, msg.value);

		}

		static void ProcessCrestronMessage(string response)
		{
			// Write the response to the console.  
            Console.WriteLine("Response received : {0}", response);

			// Wrap the response in a new xml element to avoid having "multiple root elements"
			response = string.Format("<root>{0}</root>", response);

			XmlDocument msg = new XmlDocument();
    		msg.LoadXml(response);

			// Connect Request
			if (msg.SelectSingleNode("//status") != null)
			{
				string status = msg.SelectSingleNode("//status").InnerText;
				Console.WriteLine("Status msg : {0}", status);
				if (status == "02")
				{
					// Ready to Connect
					Console.WriteLine("Ready to Connect");
					connectionRequest();
				}
				
			}
			// Update Request
			else if (msg.SelectSingleNode("//code") != null)
			{
				string code = msg.SelectSingleNode("//code").InnerText;
				Console.WriteLine("Code msg : {0}", code);
				if (code == "0")
				{
					// Successfully Connected
					Console.WriteLine("Successfully Connected!");
					updateRequest();

					running = true;

					Thread hb = new Thread(StartHeartbeats);
					hb.Start();

				}
			}
			// Heartbeat
			else if (msg.SelectSingleNode("//heartbeatResponse") != null)
			{
				Console.WriteLine("Heartbeat Response");
				//heartbeatResponse();
			}
			// Disconnect Request
			else if (msg.SelectSingleNode("//disconnectRequest") != null)
			{
				Console.WriteLine("Disconnect Request");
				running = false;
				crestronClient.disconnect();
			}
			// Disconnect Request
			else if (msg.SelectSingleNode("//data") != null)
			{
				Console.WriteLine("Data Received");
				// Do something with the data
			}
			else
			{
				Console.WriteLine("Unknown msg : {0}", msg);
			}
		}

		private static void connectionRequest()
		{
			// Send a connection request
			Console.WriteLine("Sending Connection Request...");
			string msg = string.Format(@"<cresnet><control><comm><connectRequest><passcode>{0}</passcode><mode isAuthenticationRequired=""false"" isDigitalRepeatSupported=""true"" isHeartbeatSupported=""true"" isProgramReadySupported=""true"" isUnicodeSupported=""true""></mode><device><product>Crestron Mobile Android</product><version> 1.00.01.42</version><maxExtendedLengthPacketMask>3</maxExtendedLengthPacketMask></device></connectRequest></comm></control></cresnet>", passcode);
			crestronClient.Send(msg);
		}

		private static void updateRequest()
		{
			// Send an update request
			Console.WriteLine("Sending Update Request...");
			string msg = string.Format(@"<cresnet><data eom=""false"" som=""false""><updateCommand><updateRequest></updateRequest></updateCommand></data></cresnet>");
			crestronClient.Send(msg);
		}

		private static void heartbeatRequest()
		{
			// Send a connection request
			Console.WriteLine("Sending Heartbeat Request...");
			string msg = string.Format(@"<cresnet><control><comm><heartbeatRequest></heartbeatRequest></comm></control></cresnet>");
			crestronClient.Send(msg);
		}

		private static void volumeUp()
		{
			// Send a connection request
			Console.WriteLine("Sending Volume Up...");
			buttonPress(CrestronButtonID.volume_up);
		}

		private static void volumeDown()
		{
			// Send a connection request
			Console.WriteLine("Sending Volume Down...");
			buttonPress(CrestronButtonID.volume_down);
		}

		private static void buttonPress(CrestronButtonID button_id)
		{
			sendCrestronData(CrestronDataType.digital, button_id, "true");
			sendCrestronData(CrestronDataType.digital, button_id, "false");
		}

		private static void sendCrestronData(CrestronDataType data_type, CrestronButtonID button_id, string value, string repeat = "true")
		{
			string msg = String.Empty;

			switch(data_type)
			{
				case CrestronDataType.analog:
					msg = string.Format(@"<cresnet><data eom=""false"" handle=""3"" slot=""0"" som=""false""><i32 id=""{0}"" value=""{1}"" repeating=""{2}""/></data></cresnet>", (int)button_id, value, repeat);
					break;

				case CrestronDataType.digital:
					msg = string.Format(@"<cresnet><data eom=""false"" handle=""3"" slot=""0"" som=""false""><bool id=""{0}"" value=""{1}"" repeating=""{2}""/></data></cresnet>", (int)button_id, value, repeat);
					break;

				case CrestronDataType.serial:
					msg = string.Format(@"<cresnet><data eom=""false"" handle=""3"" slot=""0"" som=""false""><string id=""{0}"" value=""{1}"" repeating=""{2}""/></data></cresnet>", (int)button_id, value, repeat);
					break;

				default:
					throw new System.FormatException("Unknown data type received");
			}

			crestronClient.Send(msg);
		}

	}
}
