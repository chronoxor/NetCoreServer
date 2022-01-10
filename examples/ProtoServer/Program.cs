using System;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;

namespace ProtoServer
{
    public class SimpleProtoSessionSender : FBE.simple.Sender, FBE.simple.ISenderListener
    {
        public SimpleProtoSession Session { get; }

        public SimpleProtoSessionSender(SimpleProtoSession session) { Session = session; }

        public long OnSend(byte[] buffer, long offset, long size)
        {
            return Session.SendAsync(buffer, offset, size) ? size : 0;
        }
    }

    public class SimpleProtoSessionReceiver : FBE.simple.Receiver, FBE.simple.IReceiverListener
    {
        public SimpleProtoSession Session { get; }

        public SimpleProtoSessionReceiver(SimpleProtoSession session) { Session = session; }

        public void OnReceive(simple.DisconnectRequest request) { Session.OnReceive(request); }
        public void OnReceive(simple.SimpleRequest request) { Session.OnReceive(request); }
    }

    public class SimpleProtoSession : TcpSession
    {
        public SimpleProtoSessionSender Sender { get; }
        public SimpleProtoSessionReceiver Receiver { get; }

        public SimpleProtoSession(TcpServer server) : base(server)
        {
            Sender = new SimpleProtoSessionSender(this);
            Receiver = new SimpleProtoSessionReceiver(this);
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"TCP protocol session with Id '{Id}' connected to remote address '{(Socket.RemoteEndPoint as IPEndPoint)?.Address}' and port {(Socket.RemoteEndPoint as IPEndPoint)?.Port}");

            // Send invite notification
            simple.SimpleNotify notify = simple.SimpleNotify.Default;
            notify.Notification = "Hello from Simple protocol server! Please send a message or '!' to disconnect the client!";
            Sender.Send(notify);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"TCP protocol session with Id '{Id}' disconnected");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Receiver.Receive(buffer, offset, size);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"TCP protocol session with Id '{Id}' caught a socket error: {error}");
        }

        // Protocol handlers
        public void OnReceive(simple.DisconnectRequest request) { Disconnect(); }
        public void OnReceive(simple.SimpleRequest request) 
        {
            Console.WriteLine($"Received: {request}");

            // Validate request
            if (string.IsNullOrEmpty(request.Message))
            {
                // Send reject
                simple.SimpleReject reject = simple.SimpleReject.Default;
                reject.id = request.id;
                reject.Error = "Request message is empty!";
                Sender.Send(reject);
                return;
            }

            // Send response
            simple.SimpleResponse response = simple.SimpleResponse.Default;
            response.id = request.id;
            response.Hash = (uint)request.Message.GetHashCode();
            response.Length = (uint)request.Message.Length;
            Sender.Send(response);
        }
    }

    public class SimpleProtoSender : FBE.simple.Sender, FBE.simple.ISenderListener
    {
        public SimpleProtoServer Server { get; }

        public SimpleProtoSender(SimpleProtoServer server) { Server = server; }

        public long OnSend(byte[] buffer, long offset, long size)
        {
            Server.Multicast(buffer, offset, size);
            return size;
        }
    }

    public class SimpleProtoServer : TcpServer
    {
        public SimpleProtoSender Sender { get; }

        public SimpleProtoServer(IPAddress address, int port) : base(address, port)
        {
            Sender = new SimpleProtoSender(this);
        }

        protected override TcpSession CreateSession() { return new SimpleProtoSession(this); }

        protected override void OnStarted()
        {
            Console.WriteLine($"Simple protocol server with Id '{Id}' started!");
        }

        protected override void OnStopped()
        {
            Console.WriteLine($"Simple protocol server with Id '{Id}' stopped!");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Simple protocol server with Id '{Id}' caught an error: {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Simple protocol server port
            int port = 4444;
            if (args.Length > 0)
                port = int.Parse(args[0]);

            Console.WriteLine($"Simple protocol server port: {port}");

            Console.WriteLine();

            // Create a new simple protocol server
            var server = new SimpleProtoServer(IPAddress.Any, port);

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
                    continue;
                }

                // Multicast admin notification to all sessions
                simple.SimpleNotify notify = simple.SimpleNotify.Default;
                notify.Notification = "(admin) " + line;
                server.Sender.Send(notify);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
