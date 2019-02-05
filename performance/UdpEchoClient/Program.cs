using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;
using NDesk.Options;

namespace UdpEchoClient
{
    class EchoClient : NetCoreServer.UdpClient
    {
        public bool Connected { get; set; }

        public EchoClient(string address, int port, int messages) : base(address, port)
        {
            _messages = messages;
        }

        protected override void OnConnected()
        {
            Connected = true;

            // Start receive datagrams
            ReceiveAsync();

            SendMessage();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            Program.TimestampStop = DateTime.UtcNow;
            Program.TotalBytes += size;
            ++Program.TotalMessages;

            // Continue receive datagrams
            ReceiveAsync();

            SendMessage();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Client caught an error with code {error}");
            ++Program.TotalErrors;
        }

        private void SendMessage()
        {
            if (_messages-- > 0)
                Send(Program.MessageToSend);
            else
                Disconnect();
        }

        private int _messages;
    }

    class Program
    {
        public static byte[] MessageToSend;
        public static DateTime TimestampStart;
        public static DateTime TimestampStop;
        public static long TotalErrors;
        public static long TotalBytes;
        public static long TotalMessages;

        static void Main(string[] args)
        {
            bool help = false;
            string address = "127.0.0.1";
            int port = 3333;
            int clients = 100;
            int messages = 1000000;
            int size = 32;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "a|address=", v => address = v },
                { "p|port=", v => port = int.Parse(v) },
                { "c|clients=", v => clients = int.Parse(v) },
                { "m|messages=", v => messages = int.Parse(v) },
                { "s|size=", v => size = int.Parse(v) }
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
            Console.WriteLine($"Working clients: {clients}");
            Console.WriteLine($"Messages to send: {messages}");
            Console.WriteLine($"Message size: {size}");

            // Prepare a message to send
            MessageToSend = new byte[size];

            // Create echo clients
            var echoClients = new List<EchoClient>();
            for (int i = 0; i < clients; ++i)
            {
                var client = new EchoClient(address, port, messages / clients);
                echoClients.Add(client);
            }

            TimestampStart = DateTime.UtcNow;

            // Connect clients
            Console.Write("Clients connecting...");
            foreach (var client in echoClients)
                client.Connect();
            Console.WriteLine("Done!");
            foreach (var client in echoClients)
            {
                while (!client.Connected)
                    Thread.Yield();
            }
            Console.WriteLine("All clients connected!");

            // Wait for processing all messages
            Console.Write("Processing...");
            foreach (var client in echoClients)
            {
                while (client.IsConnected)
                    Thread.Sleep(100);
            }
            Console.WriteLine("Done!");

            Console.WriteLine();

            Console.WriteLine($"Errors: {TotalErrors}");

            Console.WriteLine();

            Console.WriteLine($"Round-trip time: {Utilities.GenerateTimePeriod((TimestampStop - TimestampStart).TotalMilliseconds)}");
            Console.WriteLine($"Total data: {Utilities.GenerateDataSize(TotalBytes)}");
            Console.WriteLine($"Total messages: {TotalMessages}");
            Console.WriteLine($"Data throughput: {Utilities.GenerateDataSize((long)(TotalBytes / (TimestampStop - TimestampStart).TotalSeconds))}/s");
            if (TotalMessages > 0)
            {
                Console.WriteLine($"Message latency: {Utilities.GenerateTimePeriod((TimestampStop - TimestampStart).TotalMilliseconds / TotalMessages)}");
                Console.WriteLine($"Message throughput: {(long)(TotalMessages / (TimestampStop - TimestampStart).TotalSeconds)} msg/s");
            }
        }
    }
}
