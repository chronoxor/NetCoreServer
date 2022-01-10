using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using NetCoreServer;
using NDesk.Options;

namespace HttpsTraceClient
{
    class HttpsTraceClient : HttpsClient
    {
        public HttpsTraceClient(SslContext context, string address, int port, int messages) : base(context, address, port)
        {
            _messages = messages;
        }

        public void SendMessage() { SendRequestAsync(Request.MakeTraceRequest("/")); }

        protected override void OnHandshaked()
        {
            for (long i = _messages; i > 0; i--)
                SendMessage();
        }
        protected override void OnSent(long sent, long pending)
        {
            _sent += sent;
            base.OnSent(sent, pending);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _received += size;
            Program.TimestampStop = DateTime.UtcNow;
            Program.TotalBytes += size;
            base.OnReceived(buffer, offset, size);
        }

        protected override void OnReceivedResponse(HttpResponse response)
        {
            if (response.Status == 200)
                Program.TotalMessages++;
            else
                Program.TotalErrors++;
            SendMessage();
        }

        protected override void OnReceivedResponseError(HttpResponse response, string error)
        {
            Console.WriteLine($"Response error: {error}");
            Program.TotalErrors++;
            SendMessage();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Client caught an error with code {error}");
            Program.TotalErrors++;
        }

        private long _sent = 0;
        private long _received = 0;
        private long _messages = 0;
    }
    class Program
    {
        public static DateTime TimestampStart = DateTime.UtcNow;
        public static DateTime TimestampStop = DateTime.UtcNow;
        public static long TotalErrors;
        public static long TotalBytes;
        public static long TotalMessages;

        static void Main(string[] args)
        {
            bool help = false;
            string address = "127.0.0.1";
            int port = 8443;
            int clients = 100;
            int messages = 1;
            int seconds = 10;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "a|address=", v => address = v },
                { "p|port=", v => port = int.Parse(v) },
                { "c|clients=", v => clients = int.Parse(v) },
                { "m|messages=", v => messages = int.Parse(v) },
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

            Console.WriteLine($"Server address: {address}");
            Console.WriteLine($"Server port: {port}");
            Console.WriteLine($"Working clients: {clients}");
            Console.WriteLine($"Working messages: {messages}");
            Console.WriteLine($"Seconds to benchmarking: {seconds}");

            Console.WriteLine();

            // Create and prepare a new SSL client context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("client.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);

            // Create HTTPS clients
            var httpsClients = new List<HttpsTraceClient>();
            for (int i = 0; i < clients; i++)
            {
                var client = new HttpsTraceClient(context, address, port, messages);
                // client.OptionNoDelay = true;
                httpsClients.Add(client);
            }

            TimestampStart = DateTime.UtcNow;

            // Connect clients
            Console.Write("Clients connecting...");
            foreach (var client in httpsClients)
                client.ConnectAsync();
            Console.WriteLine("Done!");
            foreach (var client in httpsClients)
                while (!client.IsHandshaked)
                    Thread.Yield();
            Console.WriteLine("All clients connected!");

            // Wait for benchmarking
            Console.Write("Benchmarking...");
            Thread.Sleep(seconds * 1000);
            Console.WriteLine("Done!");

            // Disconnect clients
            Console.Write("Clients disconnecting...");
            foreach (var client in httpsClients)
                client.DisconnectAsync();
            Console.WriteLine("Done!");
            foreach (var client in httpsClients)
                while (client.IsConnected)
                    Thread.Yield();
            Console.WriteLine("All clients disconnected!");

            Console.WriteLine();

            Console.WriteLine($"Errors: {TotalErrors}");

            Console.WriteLine();

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
