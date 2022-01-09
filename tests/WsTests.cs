using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;
using Xunit;

namespace tests
{
    class EchoWsClient : WsClient
    {
        public bool IsWsConnected { get; set; }
        public bool Connected { get; set; }
        public bool Disconnected { get; set; }
        public int Received { get; set; }
        public bool Errors { get; set; }

        public EchoWsClient(string address, int port) : base(address, port) {}

        public override void OnWsConnecting(HttpRequest request)
        {
            request.SetBegin("GET", "/");
            request.SetHeader("Host", "localhost");
            request.SetHeader("Origin", "http://localhost");
            request.SetHeader("Upgrade", "websocket");
            request.SetHeader("Connection", "Upgrade");
            request.SetHeader("Sec-WebSocket-Key", Convert.ToBase64String(WsNonce));
            request.SetHeader("Sec-WebSocket-Protocol", "chat, superchat");
            request.SetHeader("Sec-WebSocket-Version", "13");
            request.SetBody();
        }
        public override void OnWsConnected(HttpResponse response) { IsWsConnected = true; Connected = true; }
        public override void OnWsDisconnected() { IsWsConnected = false; Disconnected = true; }
        public override void OnWsReceived(byte[] buffer, long offset, long size) { Received += (int)size; }

        protected override void OnError(SocketError error) { Errors = true; }
    }

    class EchoWsSession : WsSession
    {
        public bool Connected { get; set; }
        public bool Disconnected { get; set; }
        public bool Errors { get; set; }

        public EchoWsSession(WsServer server) : base(server) {}

        public override void OnWsConnected(HttpResponse response) { Connected = true; }
        public override void OnWsDisconnected() { Disconnected = true; }
        public override void OnWsReceived(byte[] buffer, long offset, long size) { SendBinaryAsync(buffer, offset, size); }

        protected override void OnError(SocketError error) { Errors = true; }
    }

    class EchoWsServer : WsServer
    {
        public bool Started { get; set; }
        public bool Stopped { get; set; }
        public bool Connected { get; set; }
        public bool Disconnected { get; set; }
        public int Clients { get; set; }
        public bool Errors { get; set; }

        public EchoWsServer(IPAddress address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new EchoWsSession(this); }

        protected override void OnStarted() { Started = true; }
        protected override void OnStopped() { Stopped = true; }
        protected override void OnConnected(TcpSession session) { Connected = true; Clients++; }
        protected override void OnDisconnected(TcpSession session) { Disconnected = true; Clients = Math.Max(Clients - 1, 0); }
        protected override void OnError(SocketError error) { Errors = true; }
    }

    public class WsTests
    {
        [Fact(DisplayName = "WebSocket server test")]
        public void WsServerTest()
        {
            string address = "127.0.0.1";
            int port = 8081;

            // Create and start Echo server
            var server = new EchoWsServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create and connect Echo client
            var client = new EchoWsClient(address, port);
            Assert.True(client.ConnectAsync());
            while (!client.IsWsConnected || (server.Clients != 1))
                Thread.Yield();

            // Send a message to the Echo server
            client.SendTextAsync("test");

            // Wait for all data processed...
            while (client.Received != 4)
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client.CloseAsync(1000));
            while (client.IsWsConnected || (server.Clients != 0))
                Thread.Yield();

            // Stop the Echo server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the Echo server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.Connected);
            Assert.True(server.Disconnected);
            Assert.True(server.BytesSent > 0);
            Assert.True(server.BytesReceived > 0);
            Assert.True(!server.Errors);

            // Check the Echo client state
            Assert.True(client.Connected);
            Assert.True(client.Disconnected);
            Assert.True(client.BytesSent > 0);
            Assert.True(client.BytesReceived > 0);
            Assert.True(!client.Errors);
        }

        [Fact(DisplayName = "WebSocket server multicast test")]
        public void WsServerMulticastTest()
        {
            string address = "127.0.0.1";
            int port = 8082;

            // Create and start Echo server
            var server = new EchoWsServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create and connect Echo client
            var client1 = new EchoWsClient(address, port);
            Assert.True(client1.ConnectAsync());
            while (!client1.IsWsConnected || (server.Clients != 1))
                Thread.Yield();

            // Multicast some data to all clients
            server.MulticastText("test");

            // Wait for all data processed...
            while (client1.Received != 4)
                Thread.Yield();

            // Create and connect Echo client
            var client2 = new EchoWsClient(address, port);
            Assert.True(client2.ConnectAsync());
            while (!client2.IsWsConnected || (server.Clients != 2))
                Thread.Yield();

            // Multicast some data to all clients
            server.MulticastText("test");

            // Wait for all data processed...
            while ((client1.Received != 8) || (client2.Received != 4))
                Thread.Yield();

            // Create and connect Echo client
            var client3 = new EchoWsClient(address, port);
            Assert.True(client3.ConnectAsync());
            while (!client3.IsWsConnected || (server.Clients != 3))
                Thread.Yield();

            // Multicast some data to all clients
            server.MulticastText("test");

            // Wait for all data processed...
            while ((client1.Received != 12) || (client2.Received != 8) || (client3.Received != 4))
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client1.CloseAsync(1000));
            while (client1.IsWsConnected || (server.Clients != 2))
                Thread.Yield();

            // Multicast some data to all clients
            server.MulticastText("test");

            // Wait for all data processed...
            while ((client1.Received != 12) || (client2.Received != 12) || (client3.Received != 8))
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client2.CloseAsync(1000));
            while (client2.IsWsConnected || (server.Clients != 1))
                Thread.Yield();

            // Multicast some data to all clients
            server.MulticastText("test");

            // Wait for all data processed...
            while ((client1.Received != 12) || (client2.Received != 12) || (client3.Received != 12))
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client3.CloseAsync(1000));
            while (client3.IsWsConnected || (server.Clients != 0))
                Thread.Yield();

            // Stop the Echo server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the Echo server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.Connected);
            Assert.True(server.Disconnected);
            Assert.True(server.BytesSent > 0);
            Assert.True(server.BytesReceived > 0);
            Assert.True(!server.Errors);

            // Check the Echo client state
            Assert.True(client1.BytesSent > 0);
            Assert.True(client2.BytesSent > 0);
            Assert.True(client3.BytesSent > 0);
            Assert.True(client1.BytesReceived > 0);
            Assert.True(client2.BytesReceived > 0);
            Assert.True(client3.BytesReceived > 0);
            Assert.True(!client1.Errors);
            Assert.True(!client2.Errors);
            Assert.True(!client3.Errors);
        }

        [Fact(DisplayName = "WebSocket server random test")]
        public void WsServerRandomTest()
        {
            string address = "127.0.0.1";
            int port = 8083;

            // Create and start Echo server
            var server = new EchoWsServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Test duration in seconds
            int duration = 10;

            // Clients collection
            var clients = new List<EchoWsClient>();

            // Start random test
            var rand = new Random();
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < duration)
            {
                // Disconnect all clients
                if ((rand.Next() % 1000) == 0)
                {
                    server.CloseAll(1000);
                }
                // Create a new client and connect
                else if ((rand.Next() % 100) == 0)
                {
                    if (clients.Count < 100)
                    {
                        // Create and connect Echo client
                        var client = new EchoWsClient(address, port);
                        clients.Add(client);
                        client.ConnectAsync();
                        while (!client.IsWsConnected)
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
                        if (client.IsWsConnected)
                        {
                            client.CloseAsync(1000);
                            while (client.IsWsConnected)
                                Thread.Yield();
                        }
                        else
                        {
                            client.ConnectAsync();
                            while (!client.IsWsConnected)
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
                        if (client.IsWsConnected)
                        {
                            client.ReconnectAsync();
                            while (!client.IsWsConnected)
                                Thread.Yield();
                        }
                    }
                }
                // Multicast a message to all clients
                else if ((rand.Next() % 10) == 0)
                {
                    server.MulticastText("test");
                }
                // Send a message from the random client
                else if ((rand.Next() % 1) == 0)
                {
                    if (clients.Count > 0)
                    {
                        int index = rand.Next() % clients.Count;
                        var client = clients[index];
                        if (client.IsWsConnected)
                            client.SendTextAsync("test");
                    }
                }

                // Sleep for a while...
                Thread.Sleep(1);
            }

            // Disconnect clients
            foreach (var client in clients)
            {
                client.CloseAsync(1000);
                while (client.IsWsConnected)
                    Thread.Yield();
            }

            // Stop the Echo server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the Echo server state
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
