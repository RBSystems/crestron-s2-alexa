using System;  
using System.Net;  
using System.Net.Sockets;  
using System.Threading;  
using System.Text;  
  
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

    public Action<string> ProcessResponse {get; set; }
  
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

    public void StartClient(string server, int port) {  
        // Connect to a remote device.  
        this.server = server;
        this.port = port;
        
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
  
            // Send test data to the remote device.  
            //Send(client,"This is a test<EOF>");  
            //sendDone.WaitOne();  

            while (isConnected) {
                response = String.Empty;
                
                // Receive the response from the remote device.  
                Receive();  
                receiveDone.WaitOne();  
    
                // Process the response from the server
                ProcessResponse(response);

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
  
    private static void ConnectCallback(IAsyncResult ar) {  
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

    static void ReceiveCallback( IAsyncResult ar ) {  
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
  
    private static void SendCallback(IAsyncResult ar) {  
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