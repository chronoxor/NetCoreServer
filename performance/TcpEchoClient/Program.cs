using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetCoreServer;
using NDesk.Options;

namespace TcpEchoClient
{
    class EchoClient : NetCoreServer.TcpClient
    {
        public bool Connected { get; set; }

        public EchoClient(string address, int port, int messages) : base(address, port)
        {
            _messagesOutput = messages;
            _messagesInput = messages;
        }

        protected override void OnConnected()
        {
            Connected = true;
            SendMessage();
        }

        protected override void OnSent(long sent, long pending)
        {
            _sent += sent;
            if (_sent >= Program.MessageToSend.Length)
            {
                SendMessage();
                _sent -= Program.MessageToSend.Length;
            }
        }

        protected override void OnReceived(byte[] buffer, long size)
        {
            _received += size;
            while (_received >= Program.MessageToSend.Length)
            {
                ReceiveMessage();
                _received -= Program.MessageToSend.Length;
            }

            Program.TimestampStop = DateTime.UtcNow;
            Program.TotalBytes += size;
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Client caught an error with code {error}");
            ++Program.TotalErrors;
        }

        private void SendMessage()
        {
            if (_messagesOutput-- > 0)
            {
                // Important: Use task chaining is neccessary here to avoid stack overflow with Socket.SendAsync() method! 
                _sender = _sender.ContinueWith(t => { Send(Program.MessageToSend); });
            }
        }

        void ReceiveMessage()
        {
            if (--_messagesInput == 0)
                Disconnect();
        }

        private int _messagesOutput;
        private int _messagesInput;
        private Task _sender = Task.CompletedTask;
        private long _sent;
        private long _received;
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
            int port = 1111;
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
                // client.OptionNoDelay = true;
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

            TotalMessages = TotalBytes / size;

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
