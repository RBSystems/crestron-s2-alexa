using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

/*
telnet Crestron 41795
https://github.com/stealthflyer/CrestronXPanelApp
https://github.com/sintax1/crestron-s2-alexa/blob/craig-asynctcp/Alexa%20S2/Crestron.cs

AddSlave 0x03 127.0.0.1 0x03
REMSlave 0x03 127.0.0.1 0x03
IPTable
reboot
*/

namespace Crestron_Controller
{

    enum CIPTypes {
        START = 0x01,
        IPID = 0x02,
        DISCONNECT = 0x03,
        DATA = 0x05,
        HEARTBEAT_TIMEOUT = 0x0d,
        HEARTBEAT_RESPONSE = 0x0e,
        CONNECT_MESSAGE = 0x0f

    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    struct CIPMessage {
        public byte Type;
        public ushort Length;
        [MarshalAs(UnmanagedType.ByValArray)]
        public byte[] Data;

        public CIPMessage(CIPTypes type, byte[] data) : this()
        {
            this.Type = (byte)type;
            this.Length = (ushort)data.Length;
            //this.Data = encode(data);
            this.Data = data;
        }

        public byte[] encode(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)Char.GetNumericValue(Convert.ToChar(data[i] % 256));
            }

            return data;
        }
    }

    class Program
    {
        private static byte PID = 0x03;
        private static string CrestronIP = "192.168.7.102";
        private static int Port = 41794;
        private static TcpClient client;      
        private static NetworkStream stream;

        static void Main(string[] args)
        {
            try 
            {
                client = new TcpClient(CrestronIP, Port);
                stream = client.GetStream();

                recvCrestronMsg();
                Thread.Sleep(1000);
                
                recvCrestronMsg();
                Thread.Sleep(1000);

                sendHeartbeat();
                //Thread.Sleep(1000);
                recvCrestronMsg();
                
                //recvCrestronMsg();
                
                // Close everything.
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

        static void sendTCPData(byte[] data) 
        {
        
            // Send the message to the connected TcpServer. 
            stream.Write(data, 0, data.Length);
            Console.WriteLine("Sent: {0}", BitConverter.ToString(data).Replace("-",@"\x"));                
        }

        static void recvCrestronMsg() 
        {
            // Buffer to store the response bytes.
            byte[] data = new Byte[256];

            // Read the first batch of the TcpServer response bytes.
            Int32 bytes = stream.Read(data, 0, data.Length);

            //Console.WriteLine("Bytes Read: {0}", bytes);

            // process the data received
            processCrestronMessage(data.Take(bytes).ToArray());
        }

        private static void processCrestronProbeResponse(string srcIP, string srcPort, byte[] buffer)
        {
            // Ignore our own packets
            if (IsLocalIpAddress(srcIP)) return;
            
            // Parse the hostname
            var hostname = buffer.Skip(10).Take(256).ToArray();
            // Remove null padding
            hostname = hostname.TakeWhile((v, index) => hostname.Skip(index).Any(w => w != 0x00)).ToArray();
            Console.WriteLine("Hostname: {0}", Encoding.UTF8.GetString(hostname));

            // IP
            Console.WriteLine("IP: {0}", srcIP);

            // Port
            Console.WriteLine("Port: {0}", srcPort);

            // Parse info
            var info = buffer.Skip(266).Take(128).ToArray();
            // Remove null padding
            info = info.TakeWhile((v, index) => info.Skip(index).Any(w => w != 0x00)).ToArray();
            //Console.WriteLine("Info: {0}\n", Encoding.UTF8.GetString(info));

            //Console.WriteLine(Encoding.UTF8.GetString(buffer));
        }

        private static Task recvUDP(ref IPEndPoint endPoint, ref UdpClient udp)
        {
            while (true)
                {
                    var recvBuffer = udp.Receive(ref endPoint);
                    processCrestronProbeResponse(
                        endPoint.Address.ToString(),
                        endPoint.Port.ToString(),
                        recvBuffer);
                }
        }

        private static void sendCrestronProbe()
        {
            IPEndPoint sendEndPoint = new IPEndPoint(IPAddress.Broadcast, Port);
            IPEndPoint RecvEndPoint = new IPEndPoint(IPAddress.Any, Port);

            // Listen client
            UdpClient udp = new UdpClient();
            udp.Client.Bind(RecvEndPoint);

            var recvTask = Task.Run(() =>
            {
                return recvUDP(ref RecvEndPoint, ref udp);
            });

            string pkt_probe = "\x14\x00\x00\x00\x01\x04\x00\x03\x00\x00" + "Probe" + new String('\x00', (256 - 5));
            byte[] sendBytes4 = Encoding.ASCII.GetBytes(pkt_probe);

            Console.WriteLine("Sending Probe...");
            udp.Send(sendBytes4, sendBytes4.Length, sendEndPoint);

            // Wait 5 seconds for devices to respond to probe request
            recvTask.Wait(TimeSpan.FromMilliseconds(5000));

            udp.Close();
        }

        private static void sendHeartbeat()
        {
            byte[] data = {0x00, 0x00};
            CIPMessage msg = new CIPMessage(CIPTypes.HEARTBEAT_TIMEOUT, data);
            byte[] sendBytes4 = CIPMsgToBytes(msg);
            sendTCPData(sendBytes4);
        }

        private static void sendCIPConnect()
        {
            byte[] data = {0x7F, 0x00, 0x00, 0x01, 0x00, PID, 0x40};  // 127.0.0.1
            CIPMessage msg = new CIPMessage(CIPTypes.START, data);
            byte[] sendBytes4 = CIPMsgToBytes(msg);
            sendTCPData(sendBytes4);
        }

        private static byte[] CIPMsgToBytes(CIPMessage msg)
        {
            byte[] data = new byte[sizeof(byte) + sizeof(uint) + msg.Length];
            data[0] = msg.Type;
            data[1] = (byte)(msg.Length >> 8);
            data[2] = (byte)(msg.Length & 0xff);
            Buffer.BlockCopy(msg.Data, 0, data, 3, msg.Length);
            
            return data;
        }

        private static void processCrestronMessage(byte[] data)
        {

            Console.WriteLine("RECV: {0}", BitConverter.ToString(data));

            //CIPMessage msg = BytesToStruct<CIPMessage>(ref data, Endianness.BigEndian);
            CIPMessage msg = new CIPMessage();
            byte[] length = data.Skip(1).Take(2).ToArray();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(length);

            msg.Type = data.Take(1).ToArray()[0];
            msg.Length = BitConverter.ToUInt16(length, 0);
            msg.Data = data.Skip(3).ToArray();

            //Console.WriteLine("Type: {0}", msg.Type);
            //Console.WriteLine("Length: {0}", msg.Length);
            //Console.WriteLine("Data: {0}", BitConverter.ToString(msg.Data).Replace("-",@"\x"));

            if (msg.Type == 0x02)
            {
                if (msg.Data[0] == 0xff && msg.Data[1] == 0xff && msg.Data[2] == 0x02)
                    Console.WriteLine("ERROR: Bad Crestron ID : {0}", PID);
            }
            else if (msg.Type == 0x0f)
            {
                if (msg.Length == 1 && msg.Data[0] == 0x02) {
                    Console.WriteLine("Connection Start");
                    sendCIPConnect();
                }
                else{
                    Console.WriteLine("Error: Bad Registration");
                    System.Environment.Exit(1);
                }
            }
            else{
                Console.WriteLine("Possible Good PID : {0}", PID);
                //System.Environment.Exit(1);
            }
        }

        public static bool IsLocalIpAddress(string host)
        {
        try
        { // get host IP addresses
            IPAddress[] hostIPs = Dns.GetHostAddresses(host);
            // get local IP addresses
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

            // test if any host IP equals to any local IP or to localhost
            foreach (IPAddress hostIP in hostIPs)
            {
                // is localhost
                if (IPAddress.IsLoopback(hostIP)) return true;
                // is local address
                foreach (IPAddress localIP in localIPs)
                {
                    if (hostIP.Equals(localIP)) return true;
                }
            }
        }
        catch { }
        return false;
        }

    }
}
