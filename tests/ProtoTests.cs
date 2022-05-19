using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;
using Xunit;

using com.chronoxor.simple;
using com.chronoxor.simple.FBE;

namespace tests
{
    class TcpProtoClient : NetCoreServer.TcpClient
    {
        public bool Conected { get; set; }
        public bool Disconected { get; set; }
        public bool Errors { get; set; }

        public TcpProtoClient(string address, int port) : base(address, port) {}

        public delegate void ConnectedHandler();
        public event ConnectedHandler Connected = () => {};

        protected override void OnConnected()
        {
            Conected = true;
            Connected?.Invoke();
        }

        public delegate void DisconnectedHandler();
        public event DisconnectedHandler Disconnected = () => {};

        protected override void OnDisconnected()
        {
            Disconected = true;
            Disconnected?.Invoke();
        }

        public delegate void ReceivedHandler(byte[] buffer, long offset, long size);
        public event ReceivedHandler Received = (buffer, offset, size) => {};

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Received?.Invoke(buffer, offset, size);
        }

        protected override void OnError(SocketError error) { Errors = true; }
    }

    class ProtoClient : Client, ISenderListener, IReceiverListener, IDisposable
    {
        private readonly TcpProtoClient _tcpProtoClient;

        public Guid Id => _tcpProtoClient.Id;
        public bool IsConnected => _tcpProtoClient.IsConnected;
        public TcpProtoClient TcpClient => _tcpProtoClient;

        public ProtoClient(string address, int port)
        {
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

        #region Connection handlers

        public delegate void ConnectedHandler();
        public event ConnectedHandler Connected = () => {};

        private void OnConnected()
        {
            // Reset FBE protocol buffers
            Reset();

            Connected?.Invoke();
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
            Receive(buffer, offset, size);
        }

        #endregion

        #region Protocol handlers

        private void HandleDisconnectRequest(DisconnectRequest request) { _tcpProtoClient.DisconnectAsync(); }
        private void HandleSimpleResponse(SimpleResponse response) {}
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
        public bool Started { get; set; }
        public bool Stopped { get; set; }
        public bool Connected { get; set; }
        public bool Disconnected { get; set; }
        public int Clients { get; set; }
        public bool Errors { get; set; }

        public ProtoSender Sender { get; }

        public ProtoServer(IPAddress address, int port) : base(address, port)
        {
            Sender = new ProtoSender(this);
        }

        protected override TcpSession CreateSession() { return new ProtoSession(this); }

        protected override void OnStarted() { Started = true; }
        protected override void OnStopped() { Stopped = true; }
        protected override void OnConnected(TcpSession session) { Connected = true; Clients++; }
        protected override void OnDisconnected(TcpSession session) { Disconnected = true; Clients--; }
        protected override void OnError(SocketError error) { Errors = true; }
    }

    public class ProtoTests
    {
        [Fact(DisplayName = "Protocol server test")]
        public void ProtoServerTest()
        {
            string address = "127.0.0.1";
            int port = 4444;

            // Create and start protocol server
            var server = new ProtoServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create and connect protocol client
            var client = new ProtoClient(address, port);
            Assert.True(client.ConnectAsync());
            while (!client.IsConnected || (server.Clients != 1))
                Thread.Yield();

            // Send a request to the protocol server
            SimpleRequest request = SimpleRequest.Default;
            request.Message = "test";
            var response = client.Request(request).Result;
            Assert.Equal(request.id, response.id);
            Assert.Equal(0u, response.Hash);
            Assert.Equal(4u, response.Length);

            // Disconnect the protocol client
            Assert.True(client.DisconnectAsync());
            while (client.IsConnected || (server.Clients != 0))
                Thread.Yield();

            // Stop the protocol server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the protocol server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.Connected);
            Assert.True(server.Disconnected);
            Assert.True(server.BytesSent > 0);
            Assert.True(server.BytesReceived > 0);
            Assert.True(!server.Errors);

            // Check the protocol client state
            Assert.True(client.TcpClient.Conected);
            Assert.True(client.TcpClient.Disconected);
            Assert.True(client.TcpClient.BytesSent > 0);
            Assert.True(client.TcpClient.BytesReceived > 0);
            Assert.True(!client.TcpClient.Errors);
        }

        [Fact(DisplayName = "Protocol multicast test")]
        public void ProtoServerMulticastTest()
        {
            string address = "127.0.0.1";
            int port = 4442;

            // Create and start protocol server
            var server = new ProtoServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create and connect protocol client
            var client1 = new ProtoClient(address, port);
            Assert.True(client1.ConnectAsync());
            while (!client1.IsConnected || (server.Clients != 1))
                Thread.Yield();

            // Create a server notification
            SimpleNotify notify = SimpleNotify.Default;
            notify.Notification = "test";

            // Multicast the notification to all clients
            server.Sender.Send(notify);

            // Wait for all data processed...
            while (client1.TcpClient.BytesReceived == 0)
                Thread.Yield();

            // Create and connect protocol client
            var client2 = new ProtoClient(address, port);
            Assert.True(client2.ConnectAsync());
            while (!client2.IsConnected || (server.Clients != 2))
                Thread.Yield();

            // Multicast the notification to all clients
            server.Sender.Send(notify);

            // Wait for all data processed...
            while (client2.TcpClient.BytesReceived == 0)
                Thread.Yield();

            // Create and connect protocol client
            var client3 = new ProtoClient(address, port);
            Assert.True(client3.ConnectAsync());
            while (!client3.IsConnected || (server.Clients != 3))
                Thread.Yield();

            // Multicast the notification to all clients
            server.Sender.Send(notify);

            // Wait for all data processed...
            while (client3.TcpClient.BytesReceived == 0)
                Thread.Yield();

            // Disconnect the protocol client
            Assert.True(client1.DisconnectAsync());
            while (client1.IsConnected || (server.Clients != 2))
                Thread.Yield();

            // Disconnect the protocol client
            Assert.True(client2.DisconnectAsync());
            while (client2.IsConnected || (server.Clients != 1))
                Thread.Yield();

            // Disconnect the protocol client
            Assert.True(client3.DisconnectAsync());
            while (client3.IsConnected || (server.Clients != 0))
                Thread.Yield();

            // Stop the protocol server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the protocol server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.Connected);
            Assert.True(server.Disconnected);
            Assert.True(server.BytesSent > 0);
            Assert.True(server.BytesReceived == 0);
            Assert.True(!server.Errors);

            // Check the protocol client state
            Assert.True(client1.TcpClient.BytesSent == 0);
            Assert.True(client2.TcpClient.BytesSent == 0);
            Assert.True(client3.TcpClient.BytesSent == 0);
            Assert.True(client1.TcpClient.BytesReceived > 0);
            Assert.True(client2.TcpClient.BytesReceived > 0);
            Assert.True(client3.TcpClient.BytesReceived > 0);
            Assert.True(!client1.TcpClient.Errors);
            Assert.True(!client2.TcpClient.Errors);
            Assert.True(!client3.TcpClient.Errors);
        }

        [Fact(DisplayName = "Protocol server random test")]
        public void TcpServerRandomTest()
        {
            string address = "127.0.0.1";
            int port = 4443;

            // Create and start protocol server
            var server = new ProtoServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Test duration in seconds
            int duration = 10;

            // Clients collection
            var clients = new List<ProtoClient>();

            // Start random test
            var rand = new Random();
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < duration)
            {
                // Disconnect all clients
                if ((rand.Next() % 1000) == 0)
                {
                    server.DisconnectAll();
                }
                // Create a new client and connect
                else if ((rand.Next() % 100) == 0)
                {
                    if (clients.Count < 100)
                    {
                        // Create and connect protocol client
                        var client = new ProtoClient(address, port);
                        clients.Add(client);
                        client.ConnectAsync();
                        while (!client.IsConnected)
                            Thread.Yield();
                    }
                }
                // Connect/Disconnect the random client
                else if ((rand.Next() % 100) == 0)
                {
                    if (clients.Count > 0)
                    {
                        int index = rand.Next() % clients.Count;
                        var client = clients[index];
                        if (client.IsConnected)
                        {
                            client.DisconnectAsync();
                            while (client.IsConnected)
                                Thread.Yield();
                        }
                        else
                        {
                            client.ConnectAsync();
                            while (!client.IsConnected)
                                Thread.Yield();
                        }
                    }
                }
                // Reconnect the random client
                else if ((rand.Next() % 100) == 0)
                {
                    if (clients.Count > 0)
                    {
                        int index = rand.Next() % clients.Count;
                        var client = clients[index];
                        if (client.IsConnected)
                        {
                            client.ReconnectAsync();
                            while (!client.IsConnected)
                                Thread.Yield();
                        }
                    }
                }
                // Multicast a notification to all clients
                else if ((rand.Next() % 10) == 0)
                {
                    SimpleNotify notify = SimpleNotify.Default;
                    notify.Notification = "test";
                    server.Sender.Send(notify);
                }
                // Send a request from the random client
                else if ((rand.Next() % 1) == 0)
                {
                    if (clients.Count > 0)
                    {
                        int index = rand.Next() % clients.Count;
                        var client = clients[index];
                        if (client.IsConnected)
                        {
                            SimpleRequest request = SimpleRequest.Default;
                            request.Message = "test";
                            client.Request(request);
                        }
                    }
                }

                // Sleep for a while...
                Thread.Sleep(1);
            }

            // Disconnect clients
            foreach (var client in clients)
            {
                client.DisconnectAsync();
                while (client.IsConnected)
                    Thread.Yield();
            }

            // Stop the protocol server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the protocol server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.Connected);
            Assert.True(server.Disconnected);
            Assert.True(server.BytesSent > 0);
            Assert.True(server.BytesReceived > 0);
            Assert.True(!server.Errors);
        }
    }
}
