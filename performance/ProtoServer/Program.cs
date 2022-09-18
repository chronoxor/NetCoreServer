using System;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using NDesk.Options;

using com.chronoxor.simple;
using com.chronoxor.simple.FBE;

namespace ProtoServer
{
    class ProtoSessionSender : Sender, ISenderListener
    {
        public ProtoSession Session { get; }

        public ProtoSessionSender(ProtoSession session) { Session = session; }

        public long OnSend(byte[] buffer, long offset, long size)
        {
            return Session.SendAsync(buffer, offset, size) ? size : 0;
        }
    }

    class ProtoSessionReceiver : Receiver, IReceiverListener
    {
        public ProtoSession Session { get; }

        public ProtoSessionReceiver(ProtoSession session) { Session = session; }

        public void OnReceive(SimpleRequest request) { Session.OnReceive(request); }
    }

    class ProtoSession : TcpSession
    {
        public ProtoSessionSender Sender { get; }
        public ProtoSessionReceiver Receiver { get; }

        public ProtoSession(TcpServer server) : base(server)
        {
            Sender = new ProtoSessionSender(this);
            Receiver = new ProtoSessionReceiver(this);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Receiver.Receive(buffer, offset, size);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Session caught an error with code {error}");
        }

        // Protocol handlers
        public void OnReceive(SimpleRequest request)
        {
            // Send response
            SimpleResponse response = SimpleResponse.Default;
            response.id = request.id;
            response.Hash = 0;
            response.Length = (uint)request.Message.Length;
            Sender.Send(response);
        }
    }

    class ProtoSender : Sender, ISenderListener
    {
        public ProtoServer Server { get; }

        public ProtoSender(ProtoServer server) { Server = server; }

        public long OnSend(byte[] buffer, long offset, long size)
        {
            Server.Multicast(buffer, offset, size);
            return size;
        }
    }

    class ProtoServer : TcpServer
    {
        public ProtoSender Sender { get; }

        public ProtoServer(IPAddress address, int port) : base(address, port)
        {
            Sender = new ProtoSender(this);
        }

        protected override TcpSession CreateSession() { return new ProtoSession(this); }

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
            int port = 4444;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "p|port=", v => port = int.Parse(v) }
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

            Console.WriteLine($"Server port: {port}");

            Console.WriteLine();

            // Create a new protocol server
            var server = new ProtoServer(IPAddress.Any, port);
            // server.OptionNoDelay = true;
            server.OptionReuseAddress = true;

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
