using System;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using NetCoreServer;

namespace SslChatClient
{
    class ChatClient : SslClient
    {
        public ChatClient(SslContext context, string address, int port) : base(context, address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat SSL client connected a new session with Id {Id}");
        }

        protected override void OnHandshaked()
        {
            Console.WriteLine($"Chat SSL client handshaked a new session with Id {Id}");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat SSL client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat SSL client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // SSL server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // SSL server port
            int port = 2222;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"SSL server address: {address}");
            Console.WriteLine($"SSL server port: {port}");

            Console.WriteLine();

            // Create and prepare a new SSL client context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("client.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);

            // Create a new SSL chat client
            var client = new ChatClient(context, address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.ConnectAsync();
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
                    client.DisconnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Send the entered text to the chat server
                client.SendAsync(line);
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
