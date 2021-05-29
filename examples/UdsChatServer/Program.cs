using NetCoreServer;
using System;
using System.Net.Sockets;
using System.Text;

namespace UdsChatServer
{
    public class ChatSession : UdsSession
    {
        public ChatSession(UdsServer server) : base(server) { }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat UDS session with Id {Id} connected!");

            // Send invite message
            var message = "Hello from UDS chat! Please send a message or '!' to disconnect the client!";
            SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat UDS session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat UDS session caught an error with code {error}");
        }
    }

    public class ChatServer : UdsServer
    {
        public ChatServer(string patch) : base(patch) { }

        protected override UdsSession CreateSession() { return new ChatSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat UDS server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // UDS patch
            var patch = @"C:\udsSock.sock";//need start with administrator rights
            if (args.Length == 1)
                patch = args[0];

            Console.WriteLine($"UDS server patch: {patch}");
            Console.WriteLine();

            // Create a new UDS chat server
            var server = new ChatServer(patch);

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (; ; )
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