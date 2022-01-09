using System;
using System.IO;
using System.Net.Sockets;
using NetCoreServer;
using NDesk.Options;

namespace UdsEchoServer
{
    class EchoSession : UdsSession
    {
        public EchoSession(UdsServer server) : base(server) {}

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Resend the message back to the client
            SendAsync(buffer, offset, size);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Session caught an error with code {error}");
        }
    }

    class EchoServer : UdsServer
    {
        public EchoServer(string path) : base(path) {}

        protected override UdsSession CreateSession() { return new EchoSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            bool help = false;
            string path = Path.Combine(Path.GetTempPath(), "echo.sock");

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "p|path=", v => path = v }
            };

            try
            {
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("Command line error: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `--help' to get usage information.");
                return;
            }

            if (help)
            {
                Console.WriteLine("Usage:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine($"Server Unix Domain Socket path: {path}");

            Console.WriteLine();

            // Create a new echo server
            var server = new EchoServer(path);

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
                }
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
