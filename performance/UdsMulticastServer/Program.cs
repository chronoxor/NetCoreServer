using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetCoreServer;
using NDesk.Options;

namespace UdsMulticastServer
{
    class MulticastSession : UdsSession
    {
        public MulticastSession(UdsServer server) : base(server) {}

        public override bool SendAsync(byte[] buffer, long offset, long size)
        {
            // Limit session send buffer to 1 megabyte
            const long limit = 1 * 1024 * 1024;
            long pending = BytesPending + BytesSending;
            if ((pending + size) > limit)
                return false;
            if (size > (limit - pending))
                size = limit - pending;

            return base.SendAsync(buffer, offset, size);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Session caught an error with code {error}");
        }
    }

    class MulticastServer : UdsServer
    {
        public MulticastServer(string path) : base(path) {}

        protected override UdsSession CreateSession() { return new MulticastSession(this); }

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
            string path = Path.Combine(Path.GetTempPath(), "multicast.sock");
            int messagesRate = 1000000;
            int messageSize = 32;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "p|path=", v => path = v },
                { "m|messages=", v => messagesRate = int.Parse(v) },
                { "s|size=", v => messageSize = int.Parse(v) }
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
            Console.WriteLine($"Messages rate: {messagesRate}");
            Console.WriteLine($"Message size: {messageSize}");

            Console.WriteLine();

            // Create a new echo server
            var server = new MulticastServer(path);

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            // Start the multicasting thread
            bool multicasting = true;
            var multicaster = Task.Factory.StartNew(() =>
            {
                // Prepare message to multicast
                byte[] message = new byte[messageSize];

                // Multicasting loop
                while (multicasting)
                {
                    var start = DateTime.UtcNow;
                    for (int i = 0; i < messagesRate; i++)
                        server.Multicast(message);
                    var end = DateTime.UtcNow;

                    // Sleep for remaining time or yield
                    var milliseconds = (int)(end - start).TotalMilliseconds;
                    if (milliseconds < 1000)
                        Thread.Sleep(1000 - milliseconds);
                    else
                        Thread.Yield();
                }
            });

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

            // Stop the multicasting thread
            multicasting = false;
            multicaster.Wait();

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
