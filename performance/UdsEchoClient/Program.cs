using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;
using NDesk.Options;

namespace UdsEchoClient
{
    class EchoClient : UdsClient
    {
        public EchoClient(string path, int messages) : base(path)
        {
            _messages = messages;
        }

        protected override void OnConnected()
        {
            for (long i = _messages; i > 0; i--)
                SendMessage();
        }

        protected override void OnSent(long sent, long pending)
        {
            _sent += sent;
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _received += size;
            while (_received >= Program.MessageToSend.Length)
            {
                SendMessage();
                _received -= Program.MessageToSend.Length;
            }

            Program.TimestampStop = DateTime.UtcNow;
            Program.TotalBytes += size;
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Client caught an error with code {error}");
            Program.TotalErrors++;
        }

        private void SendMessage()
        {
            SendAsync(Program.MessageToSend);
        }

        private long _sent;
        private long _received;
        private long _messages;
    }

    class Program
    {
        public static byte[] MessageToSend;
        public static DateTime TimestampStart = DateTime.UtcNow;
        public static DateTime TimestampStop = DateTime.UtcNow;
        public static long TotalErrors;
        public static long TotalBytes;
        public static long TotalMessages;

        static void Main(string[] args)
        {
            bool help = false;
            string path = Path.Combine(Path.GetTempPath(), "echo.sock");
            int clients = 100;
            int messages = 1000;
            int size = 32;
            int seconds = 10;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "p|path=", v => path = v },
                { "c|clients=", v => clients = int.Parse(v) },
                { "m|messages=", v => messages = int.Parse(v) },
                { "s|size=", v => size = int.Parse(v) },
                { "z|seconds=", v => seconds = int.Parse(v) }
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
            Console.WriteLine($"Working clients: {clients}");
            Console.WriteLine($"Working messages: {messages}");
            Console.WriteLine($"Message size: {size}");
            Console.WriteLine($"Seconds to benchmarking: {seconds}");

            Console.WriteLine();

            // Prepare a message to send
            MessageToSend = new byte[size];

            // Create echo clients
            var echoClients = new List<EchoClient>();
            for (int i = 0; i < clients; i++)
            {
                var client = new EchoClient(path, messages);
                echoClients.Add(client);
            }

            TimestampStart = DateTime.UtcNow;

            // Connect clients
            Console.Write("Clients connecting...");
            foreach (var client in echoClients)
                client.ConnectAsync();
            Console.WriteLine("Done!");
            foreach (var client in echoClients)
                while (!client.IsConnected)
                    Thread.Yield();
            Console.WriteLine("All clients connected!");

            // Wait for benchmarking
            Console.Write("Benchmarking...");
            Thread.Sleep(seconds * 1000);
            Console.WriteLine("Done!");

            // Disconnect clients
            Console.Write("Clients disconnecting...");
            foreach (var client in echoClients)
                client.DisconnectAsync();
            Console.WriteLine("Done!");
            foreach (var client in echoClients)
                while (client.IsConnected)
                    Thread.Yield();
            Console.WriteLine("All clients disconnected!");

            Console.WriteLine();

            Console.WriteLine($"Errors: {TotalErrors}");

            Console.WriteLine();

            TotalMessages = TotalBytes / size;

            Console.WriteLine($"Total time: {Utilities.GenerateTimePeriod((TimestampStop - TimestampStart).TotalMilliseconds)}");
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
