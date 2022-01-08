using System;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using NetCoreServer;

namespace WssChatClient
{
    class ChatClient : WssClient
    {
        public ChatClient(SslContext context, string address, int port) : base(context, address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            CloseAsync(1000);
            while (IsConnected)
                Thread.Yield();
        }

        public override void OnWsConnecting(HttpRequest request)
        {
            request.SetBegin("GET", "/");
            request.SetHeader("Host", "localhost");
            request.SetHeader("Origin", "http://localhost");
            request.SetHeader("Upgrade", "websocket");
            request.SetHeader("Connection", "Upgrade");
            request.SetHeader("Sec-WebSocket-Key", Convert.ToBase64String(WsNonce));
            request.SetHeader("Sec-WebSocket-Protocol", "chat, superchat");
            request.SetHeader("Sec-WebSocket-Version", "13");
            request.SetBody();
        }

        public override void OnWsConnected(HttpResponse response)
        {
            Console.WriteLine($"Chat WebSocket client connected a new session with Id {Id}");
        }

        public override void OnWsDisconnected()
        {
            Console.WriteLine($"Chat WebSocket client disconnected a session with Id {Id}");
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine($"Incoming: {Encoding.UTF8.GetString(buffer, (int)offset, (int)size)}");
        }

        protected override void OnDisconnected()
        {
            base.OnDisconnected();

            Console.WriteLine($"Chat WebSocket client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // WebSocket server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // WebSocket server port
            int port = 8443;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"WebSocket server address: {address}");
            Console.WriteLine($"WebSocket server port: {port}");

            Console.WriteLine();

            // Create and prepare a new SSL client context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("client.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);

            // Create a new TCP chat client
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

                // Reconnect the client
                if (line == "!")
                {
                    Console.Write("Client reconnecting...");
                    if (client.IsConnected)
                        client.ReconnectAsync();
                    else
                        client.ConnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Send the entered text to the chat server
                client.SendTextAsync(line);
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
