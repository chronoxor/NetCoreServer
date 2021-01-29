using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetCoreServer;
using NDesk.Options;

namespace UdpMulticastServer
{
    class MulticastServer : UdpServer
    {
        public MulticastServer(IPAddress address, int port) : base(address, port) {}

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
            string address = "239.255.0.1";
            int port = 3333;
            int messagesRate = 1000000;
            int messageSize = 32;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "a|address=", v => address = v },
                { "p|port=", v => port = int.Parse(v) },
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

            Console.WriteLine($"Server address: {address}");
            Console.WriteLine($"Server port: {port}");
            Console.WriteLine($"Messages rate: {messagesRate}");
            Console.WriteLine($"Message size: {messageSize}");

            Console.WriteLine();

            // Create a new echo server
            var server = new MulticastServer(IPAddress.Any, 0);
            server.OptionReuseAddress = true;

            // Start the server
            Console.Write("Server starting...");
            server.Start(address, port);
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
