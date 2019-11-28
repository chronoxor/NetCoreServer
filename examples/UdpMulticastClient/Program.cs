using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UdpClient = NetCoreServer.UdpClient;

namespace UdpMulticastClient
{
    class MulticastClient : UdpClient
    {
        public string Multicast;

        public MulticastClient(string address, int port) : base(address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            Disconnect();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Multicast UDP client connected a new session with Id {Id}");

            // Join UDP multicast group
            JoinMulticastGroup(Multicast);

            // Start receive datagrams
            ReceiveAsync();
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Multicast UDP client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                Connect();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

            // Continue receive datagrams
            ReceiveAsync();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Multicast UDP client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // UDP listen address
            string listenAddress = "0.0.0.0";
            if (args.Length > 0)
                listenAddress = args[0];

            // UDP multicast address
            string multicastAddress = "239.255.0.1";
            if (args.Length > 1)
                multicastAddress = args[1];

            // UDP multicast port
            int multicastPort = 3334;
            if (args.Length > 2)
                multicastPort = int.Parse(args[2]);

            Console.WriteLine($"UDP listen address: {listenAddress}");
            Console.WriteLine($"UDP multicast address: {multicastAddress}");
            Console.WriteLine($"UDP multicast port: {multicastPort}");

            Console.WriteLine();

            // Create a new TCP chat client
            var client = new MulticastClient(listenAddress, multicastPort);
            client.SetupMulticast(true);
            client.Multicast = multicastAddress;

            // Connect the client
            Console.Write("Client connecting...");
            client.Connect();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Disconnect the client
                if (line == "!")
                {
                    Console.Write("Client disconnecting...");
                    client.Disconnect();
                    Console.WriteLine("Done!");
                    continue;
                }
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
