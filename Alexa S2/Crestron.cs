using System;  
using System.Net;  
using System.Net.Sockets;  
using System.Threading;  
using System.Text;
using System.Xml;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crestron {
    public enum CrestronDataType
	{
		digital,
		analog,
		serial
	};

    // State object for receiving data from remote device.  
    public class StateObject {  
        // Client socket.  
        public Socket workSocket = null;  
        // Size of receive buffer.  
        public const int BufferSize = 256;  
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];  
        // Received data string.  
        public StringBuilder sb = new StringBuilder();  
    }  
    
    public class AsynchronousClient {  
        // The port number for the remote device.  
        public string server { get; set; }
        public int port { get; set; }
        public int passcode { get; set; }

        public Action<string> CrestronDataHandler {get; set; }
    
        // ManualResetEvent instances signal completion.  
        private static ManualResetEvent connectDone =   
            new ManualResetEvent(false);  
        private static ManualResetEvent sendDone =   
            new ManualResetEvent(false);  
        private static ManualResetEvent receiveDone =   
            new ManualResetEvent(false);  
    
        // The response from the remote device.  
        private static String response = String.Empty;
        private static Socket client;
        private static bool isConnected = false;
    
        public void disconnect() {
            isConnected = false;
        }

        public void StartHeartbeats()
		{
			while (isConnected)
			{
                // Send heartbeat once per minute
				Thread.Sleep(60000);
				heartbeatRequest();
			}
		}

        public void connectionRequest()
		{
			// Send a connection request
			Console.WriteLine("Sending Connection Request...");
			string msg = string.Format(@"<cresnet><control><comm><connectRequest><passcode>{0}</passcode><mode isAuthenticationRequired=""false"" isDigitalRepeatSupported=""true"" isHeartbeatSupported=""true"" isProgramReadySupported=""true"" isUnicodeSupported=""true""></mode><device><product>Crestron Mobile Android</product><version> 1.00.01.42</version><maxExtendedLengthPacketMask>3</maxExtendedLengthPacketMask></device></connectRequest></comm></control></cresnet>", passcode);
			this.Send(msg);
		}

		public void updateRequest()
		{
			// Send an update request
			Console.WriteLine("Sending Update Request...");
			string msg = string.Format(@"<cresnet><data eom=""false"" som=""false""><updateCommand><updateRequest></updateRequest></updateCommand></data></cresnet>");
			this.Send(msg);
		}

		public void heartbeatRequest()
		{
			// Send a connection request
			Console.WriteLine("Sending Heartbeat Request...");
			string msg = string.Format(@"<cresnet><control><comm><heartbeatRequest></heartbeatRequest></comm></control></cresnet>");
			this.Send(msg);
		}

        public void buttonPress(int button_id)
		{
			sendCrestronData(CrestronDataType.digital, button_id, "true");
			sendCrestronData(CrestronDataType.digital, button_id, "false");
		}

		public void sendCrestronData(CrestronDataType data_type, int button_id, string value, string repeat = "true")
		{
			string msg = String.Empty;

			switch(data_type)
			{
				case CrestronDataType.analog:
					msg = string.Format(@"<cresnet><data eom=""false"" handle=""3"" slot=""0"" som=""false""><i32 id=""{0}"" value=""{1}"" repeating=""{2}""/></data></cresnet>", button_id, value, repeat);
					break;

				case CrestronDataType.digital:
					msg = string.Format(@"<cresnet><data eom=""false"" handle=""3"" slot=""0"" som=""false""><bool id=""{0}"" value=""{1}"" repeating=""{2}""/></data></cresnet>", button_id, value, repeat);
					break;

				case CrestronDataType.serial:
					msg = string.Format(@"<cresnet><data eom=""false"" handle=""3"" slot=""0"" som=""false""><string id=""{0}"" value=""{1}"" repeating=""{2}""/></data></cresnet>", button_id, value, repeat);
					break;

				default:
					throw new System.FormatException("Unknown data type received");
			}

			this.Send(msg);
		}

        private void ProcessCrestronMessage(string response)
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
					this.connectionRequest();
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
					this.updateRequest();

					isConnected = true;

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
				this.disconnect();
			}
			// Disconnect Request
			else if (msg.SelectSingleNode("//data") != null)
			{
				Console.WriteLine("Data Received");
				// Do something with the data
                CrestronDataHandler(response);
			}
			else
			{
				Console.WriteLine("Unknown msg : {0}", msg);
			}
		}

        public void StartClient(string server, int port, int passcode) {  
            // Connect to a remote device.  
            this.server = server;
            this.port = port;
            this.passcode = passcode;
            
            try {  
                // Establish the remote endpoint for the socket.  
                IPAddress ipAddress = IPAddress.Parse(server);  
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);  
    
                // Create a TCP/IP socket.  
                client = new Socket(ipAddress.AddressFamily,  
                    SocketType.Stream, ProtocolType.Tcp);  
    
                // Connect to the remote endpoint.  
                client.BeginConnect( remoteEP,   
                    new AsyncCallback(ConnectCallback), client);  
                connectDone.WaitOne();  

                while (isConnected) {
                    response = String.Empty;
                    
                    // Receive the response from the remote device.  
                    Receive();  
                    receiveDone.WaitOne();  
        
                    // Process the response from the server
                    ProcessCrestronMessage(response);

                    Thread.Sleep(1000);

                    receiveDone.Reset();
                }
    
                // Release the socket.  
                client.Shutdown(SocketShutdown.Both);  
                client.Close();  
    
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }  
    
        private void ConnectCallback(IAsyncResult ar) {  
            try {  
                // Retrieve the socket from the state object.  
                Socket client = (Socket) ar.AsyncState;  
    
                // Complete the connection.  
                client.EndConnect(ar);  
    
                Console.WriteLine("Socket connected to {0}",  
                    client.RemoteEndPoint.ToString());  

                isConnected = true;
    
                // Signal that the connection has been made.  
                connectDone.Set();  
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }  

        private void Receive() {  
            Console.WriteLine("Waiting for data from the server...");

            try {  
                // Create the state object.  
                StateObject state = new StateObject();  
                state.workSocket = client;  
    
                // Begin receiving the data from the remote device.  
                client.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0,  
                    new AsyncCallback(ReceiveCallback), state);  
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }  

        private void ReceiveCallback( IAsyncResult ar ) {  
            try {  
                // Retrieve the state object and the client socket   
                // from the asynchronous state object.  
                StateObject state = (StateObject) ar.AsyncState;  
                Socket client = state.workSocket;  

                // Read data from the remote device.  
                int bytesRead = client.EndReceive(ar);
                Console.WriteLine("bytesRead: {0}", bytesRead);

                if (bytesRead > 0) {
                    // Append the data received to a buffer so we can receive more if we need to
                    // build a complete message in chunks
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer,0,bytesRead));

                    // convert it to a strong so we can do string operations on it
                    response = state.sb.ToString();

                    // end of crestnet xml so we know we have all of the message
                    if (response.EndsWith("</cresnet>"))
                    {
                        // signal the program to process the received message
                        receiveDone.Set(); 

                    // We haven't received the end of the message (</cresnet>) yet so we need to receive more data  
                    } else {  
                        client.BeginReceive(state.buffer,0,StateObject.BufferSize,0,  
                            new AsyncCallback(ReceiveCallback), state); 
                    }
                }
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }  
    
        public void Send(String data) {  
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            Console.WriteLine("Sending data to Crestron : {0}", data);
    
            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0,  
                new AsyncCallback(SendCallback), client);  
        }  
    
        private void SendCallback(IAsyncResult ar) {  
            try {  
                // Retrieve the socket from the state object.  
                Socket client = (Socket) ar.AsyncState;  
    
                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);  
                Console.WriteLine("Sent {0} bytes to Crestron.", bytesSent);  
    
                // Signal that all bytes have been sent.  
                sendDone.Set();  
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }  
    }  
}