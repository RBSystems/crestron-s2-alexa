using System.Net.Sockets;
using System;

namespace Alexa_S2
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("connecting");
			Connect("192.168.7.102", 41790);
			
		}
		static void readmessage(NetworkStream stream, Byte[] data)
		{

			Int32 bytes = stream.Read(data, 0, data.Length);
			String responseData = String.Empty;
			responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
			Console.WriteLine("Received: {0}", responseData); 

		}
		static void Connect(String server, Int32 port)
		{
			try
			{
	
				TcpClient client = new TcpClient(server, port);
				NetworkStream stream = client.GetStream();
				Byte[] data = new Byte[1024];

				readmessage(stream, data);

				readmessage(stream, data);

				stream.Close();         
    			client.Close(); 



					


			}
			catch (ArgumentNullException e) 
			{
				Console.WriteLine("ArgumentNullException: {0}", e);
			} 
			catch (SocketException e) 
			{
				Console.WriteLine("SocketException: {0}", e);
  			}

		}
	}
}
