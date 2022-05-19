using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;
using NDesk.Options;

using com.chronoxor.simple;
using com.chronoxor.simple.FBE;

namespace ProtoClient
{
    class TcpProtoClient : NetCoreServer.TcpClient
    {
        public TcpProtoClient(string address, int port) : base(address, port) {}

        public delegate void ConnectedHandler();
        public event ConnectedHandler Connected = () => {};

        protected override void OnConnected()
        {
            Connected?.Invoke();
        }

        public delegate void DisconnectedHandler();
        public event DisconnectedHandler Disconnected = () => {};

        protected override void OnDisconnected()
        {
            Disconnected?.Invoke();
        }

        public delegate void ReceivedHandler(byte[] buffer, long offset, long size);
        public event ReceivedHandler Received = (buffer, offset, size) => {};

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Received?.Invoke(buffer, offset, size);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Client caught an error with code {error}");
            Program.TotalErrors++;
        }
    }

    class ProtoClient : Client, ISenderListener, IReceiverListener, IDisposable
    {
        private readonly TcpProtoClient _tcpProtoClient;

        public Guid Id => _tcpProtoClient.Id;
        public bool IsConnected => _tcpProtoClient.IsConnected;
        public TcpProtoClient TcpClient => _tcpProtoClient;

        private long _messages;

        public ProtoClient(string address, int port, int messages)
        {
            _messages = messages;
            _tcpProtoClient = new TcpProtoClient(address, port);
            _tcpProtoClient.Connected += OnConnected;
            _tcpProtoClient.Disconnected += OnDisconnected;
            _tcpProtoClient.Received += OnReceived;
            ReceivedResponse_DisconnectRequest += HandleDisconnectRequest;
            ReceivedResponse_SimpleResponse += HandleSimpleResponse;
            ReceivedResponse_SimpleReject += HandleSimpleReject;
            ReceivedResponse_SimpleNotify += HandleSimpleNotify;
        }

        private void DisposeClient()
        {
            _tcpProtoClient.Connected -= OnConnected;
            _tcpProtoClient.Connected -= OnDisconnected;
            _tcpProtoClient.Received -= OnReceived;
            ReceivedResponse_DisconnectRequest -= HandleDisconnectRequest;
            ReceivedResponse_SimpleResponse -= HandleSimpleResponse;
            ReceivedResponse_SimpleReject -= HandleSimpleReject;
            ReceivedResponse_SimpleNotify -= HandleSimpleNotify;
            _tcpProtoClient.Dispose();
        }

        public bool ConnectAsync() { return _tcpProtoClient.ConnectAsync(); }
        public bool DisconnectAsync() { return _tcpProtoClient.DisconnectAsync(); }
        public bool ReconnectAsync() { return _tcpProtoClient.ReconnectAsync(); }

        private void SendMessage()
        {
            SimpleRequest message = SimpleRequest.Default;
            message.Message = Program.MessageToSend;
            Request(message);
        }

        #region Connection handlers

        public delegate void ConnectedHandler();
        public event ConnectedHandler Connected = () => {};

        private void OnConnected()
        {
            // Reset FBE protocol buffers
            Reset();

            Connected?.Invoke();

            for (long i = _messages; i > 0; i--)
                SendMessage();
        }

        public delegate void DisconnectedHandler();
        public event DisconnectedHandler Disconnected = () => {};

        private void OnDisconnected()
        {
            Disconnected?.Invoke();
        }

        public long OnSend(byte[] buffer, long offset, long size)
        {
            return _tcpProtoClient.SendAsync(buffer, offset, size) ? size : 0;
        }

        public void OnReceived(byte[] buffer, long offset, long size)
        {
            Program.TimestampStop = DateTime.UtcNow;
            Program.TotalBytes += size;

            Receive(buffer, offset, size);
        }

        #endregion

        #region Protocol handlers

        private void HandleDisconnectRequest(DisconnectRequest request) { _tcpProtoClient.DisconnectAsync(); }
        private void HandleSimpleResponse(SimpleResponse response) { SendMessage(); }
        private void HandleSimpleReject(SimpleReject reject) {}
        private void HandleSimpleNotify(SimpleNotify notify) {}

        #endregion

        #region IDisposable implementation

        // Disposed flag.
        private bool _disposed;

        // Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposingManagedResources)
        {
            // The idea here is that Dispose(Boolean) knows whether it is
            // being called to do explicit cleanup (the Boolean is true)
            // versus being called due to a garbage collection (the Boolean
            // is false). This distinction is useful because, when being
            // disposed explicitly, the Dispose(Boolean) method can safely
            // execute code using reference type fields that refer to other
            // objects knowing for sure that these other objects have not been
            // finalized or disposed of yet. When the Boolean is false,
            // the Dispose(Boolean) method should not execute code that
            // refer to reference type fields because those objects may
            // have already been finalized."

            if (!_disposed)
            {
                if (disposingManagedResources)
                {
                    // Dispose managed resources here...
                    DisposeClient();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                _disposed = true;
            }
        }

        #endregion
    }

    class Program
    {
        public static string MessageToSend;
        public static DateTime TimestampStart = DateTime.UtcNow;
        public static DateTime TimestampStop = DateTime.UtcNow;
        public static long TotalErrors;
        public static long TotalBytes;
        public static long TotalMessages;

        static void Main(string[] args)
        {
            bool help = false;
            string address = "127.0.0.1";
            int port = 4444;
            int clients = 100;
            int messages = 1000;
            int size = 32;
            int seconds = 10;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "a|address=", v => address = v },
                { "p|port=", v => port = int.Parse(v) },
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

            Console.WriteLine($"Server address: {address}");
            Console.WriteLine($"Server port: {port}");
            Console.WriteLine($"Working clients: {clients}");
            Console.WriteLine($"Working messages: {messages}");
            Console.WriteLine($"Message size: {size}");
            Console.WriteLine($"Seconds to benchmarking: {seconds}");

            Console.WriteLine();

            // Prepare a message to send
            MessageToSend = new string('\0', size);

            // Create protocol clients
            var echoClients = new List<ProtoClient>();
            for (int i = 0; i < clients; i++)
            {
                var client = new ProtoClient(address, port, messages);
                // client.TcpClient.OptionNoDelay = true;
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
