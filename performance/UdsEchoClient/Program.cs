using NDesk.Options;
using NetCoreServer;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace UdsEchoClient
{
    public delegate void Done(DateTime dateTime);

    public class EchoClient : UdsClient
    {
        public event Done OnDone;

        public EchoClient(string patch, int messages) : base(patch)
        {
            _messages = messages;
        }

        protected override void OnConnected()
        {
            for (var i = _messages; i > 0; i--)
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

            var doneTime = DateTime.UtcNow;
            Interlocked.Add(ref Program.TotalBytes, size);
            OnDone?.Invoke(doneTime);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Client caught an error with code {error}");
            Interlocked.Increment(ref Program.TotalErrors);
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
        private static object _stopTimeLock = new();
        private static AutoResetEvent _wakeEvent = new(false);

        static void Main(string[] args)
        {
            var help = false;
            var patch = @"C:\udsSock.sock";//need start with administrator rights
            var clients = 100;
            var messages = 1000;
            var size = 32;
            var seconds = 10;

            var clientsDone = 0;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "p|patch=", v => patch = v },
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

            Console.WriteLine($"Server patch: {patch}");
            Console.WriteLine($"Working clients: {clients}");
            Console.WriteLine($"Working messages: {messages}");
            Console.WriteLine($"Message size: {size}");
            Console.WriteLine($"Seconds to benchmarking: {seconds}");

            Console.WriteLine();

            // Prepare a message to send
            MessageToSend = new byte[size];

            // Create echo clients
            var echoClients = new List<EchoClient>(clients);
            for (int i = 0; i < clients; i++)
            {
                var client = new EchoClient(patch, messages);
                echoClients.Add(client);
                client.OnDone += ClientOnOnDone;
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
            _wakeEvent.WaitOne();
            Console.WriteLine("Done!");

            // Disconnect clients
            Console.Write("Clients disconnecting...");
            foreach (var client in echoClients)
                client.Disconnect();
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

            Console.WriteLine("Press Enter for Exit");
            Console.ReadLine();

            void ClientOnOnDone(DateTime datetime)
            {
                lock (_stopTimeLock)
                {
                    if (Program.TimestampStop < datetime)
                        Program.TimestampStop = datetime;

                    clientsDone++;

                    if (clientsDone == clients)
                        _wakeEvent.Set();
                }
            }
        }
    }
}