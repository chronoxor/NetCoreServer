using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace UdsChatServer
{
    class ChatSession : UdsSession
    {
        public ChatSession(UdsServer server) : base(server) {}

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat Unix Domain Socket session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from Unix Domain Socket chat! Please send a message or '!' to disconnect the client!";
            SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat Unix Domain Socket session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat Unix Domain Socket session caught an error with code {error}");
        }
    }

    class ChatServer : UdsServer
    {
        public ChatServer(string path) : base(path) {}

        protected override UdsSession CreateSession() { return new ChatSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat Unix Domain Socket server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Unix Domain Socket path
            string path = Path.Combine(Path.GetTempPath(), "chat.sock");
            if (args.Length > 0)
                path = args[0];

            Console.WriteLine($"Unix Domain Socket server path: {path}");

            Console.WriteLine();

            // Create a new Unix Domain Socket chat server
            var server = new ChatServer(path);

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Restart the server
                if (line == "!")
                {
                    Console.Write("Server restarting...");
                    server.Restart();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Multicast admin message to all sessions
                line = "(admin) " + line;
                server.Multicast(line);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
