using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using NetCoreServer;
using Xunit;

namespace tests
{
    class EchoSslClient : SslClient
    {
        public bool Connected { get; set; }
        public bool Handshaked { get; set; }
        public bool Disconnected { get; set; }
        public bool Errors { get; set; }

        public EchoSslClient(SslContext context, string address, int port) : base(context, address, port) {}

        public static SslContext CreateContext()
        {
            return new SslContext(SslProtocols.Tls12, new X509Certificate2("client.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);
        }

        protected override void OnConnected() { Connected = true; }
        protected override void OnHandshaked() { Handshaked = true; }
        protected override void OnDisconnected() { Disconnected = true; }
        protected override void OnError(SocketError error) { Errors = true; }
    }

    class EchoSslSession : SslSession
    {
        public bool Connected { get; set; }
        public bool Handshaked { get; set; }
        public bool Disconnected { get; set; }
        public bool Errors { get; set; }

        public EchoSslSession(SslServer server) : base(server) {}

        protected override void OnConnected() { Connected = true; }
        protected override void OnHandshaked() { Handshaked = true; }
        protected override void OnDisconnected() { Disconnected = true; }
        protected override void OnReceived(byte[] buffer, long offset, long size) { SendAsync(buffer, offset, size); }
        protected override void OnError(SocketError error) { Errors = true; }
    }

    class EchoSslServer : SslServer
    {
        public bool Started { get; set; }
        public bool Stopped { get; set; }
        public bool Connected { get; set; }
        public bool Handshaked { get; set; }
        public bool Disconnected { get; set; }
        public int Clients { get; set; }
        public bool Errors { get; set; }

        public EchoSslServer(SslContext context, IPAddress address, int port) : base(context, address, port) {}

        public static SslContext CreateContext()
        {
            return new SslContext(SslProtocols.Tls12, new X509Certificate2("server.pfx", "qwerty"));
        }

        protected override SslSession CreateSession() { return new EchoSslSession(this); }

        protected override void OnStarted() { Started = true; }
        protected override void OnStopped() { Stopped = true; }
        protected override void OnConnected(SslSession session) { Connected = true; }
        protected override void OnHandshaked(SslSession session) { Handshaked = true; Clients++; }
        protected override void OnDisconnected(SslSession session) { Disconnected = true; Clients--; }
        protected override void OnError(SocketError error) { Errors = true; }
    }

    public class SslTests
    {
        [Fact(DisplayName = "SSL server test")]
        public void SslServerTest()
        {
            string address = "127.0.0.1";
            int port = 2222;

            // Create and prepare a new SSL server context
            var serverContext = EchoSslServer.CreateContext();

            // Create and start Echo server
            var server = new EchoSslServer(serverContext, IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create and prepare a new SSL client context
            var clientContext = EchoSslClient.CreateContext();

            // Create and connect Echo client
            var client = new EchoSslClient(clientContext, address, port);
            Assert.True(client.ConnectAsync());
            while (!client.IsConnected || !client.IsHandshaked || (server.Clients != 1))
                Thread.Yield();

            // Send a message to the Echo server
            client.SendAsync("test");

            // Wait for all data processed...
            while (client.BytesReceived != 4)
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client.DisconnectAsync());
            while (client.IsConnected || client.IsHandshaked || (server.Clients != 0))
                Thread.Yield();

            // Stop the Echo server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the Echo server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.Connected);
            Assert.True(server.Handshaked);
            Assert.True(server.Disconnected);
            Assert.True(server.BytesSent == 4);
            Assert.True(server.BytesReceived == 4);
            Assert.True(!server.Errors);

            // Check the Echo client state
            Assert.True(client.Connected);
            Assert.True(client.Handshaked);
            Assert.True(client.Disconnected);
            Assert.True(client.BytesSent == 4);
            Assert.True(client.BytesReceived == 4);
            Assert.True(!client.Errors);
        }

        [Fact(DisplayName = "SSL server multicast test")]
        public void SslServerMulticastTest()
        {
            string address = "127.0.0.1";
            int port = 2223;

            // Create and prepare a new SSL server context
            var serverContext = EchoSslServer.CreateContext();

            // Create and start Echo server
            var server = new EchoSslServer(serverContext, IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create and prepare a new SSL client context
            var clientContext = EchoSslClient.CreateContext();

            // Create and connect Echo client
            var client1 = new EchoSslClient(clientContext, address, port);
            Assert.True(client1.ConnectAsync());
            while (!client1.IsConnected || !client1.IsHandshaked || (server.Clients != 1))
                Thread.Yield();

            // Multicast some data to all clients
            server.Multicast("test");

            // Wait for all data processed...
            while (client1.BytesReceived != 4)
                Thread.Yield();

            // Create and connect Echo client
            var client2 = new EchoSslClient(clientContext, address, port);
            Assert.True(client2.ConnectAsync());
            while (!client2.IsConnected || !client2.IsHandshaked || (server.Clients != 2))
                Thread.Yield();

            // Multicast some data to all clients
            server.Multicast("test");

            // Wait for all data processed...
            while ((client1.BytesReceived != 8) || (client2.BytesReceived != 4))
                Thread.Yield();

            // Create and connect Echo client
            var client3 = new EchoSslClient(clientContext, address, port);
            Assert.True(client3.ConnectAsync());
            while (!client3.IsConnected || !client3.IsHandshaked || (server.Clients != 3))
                Thread.Yield();

            // Multicast some data to all clients
            server.Multicast("test");

            // Wait for all data processed...
            while ((client1.BytesReceived != 12) || (client2.BytesReceived != 8) || (client3.BytesReceived != 4))
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client1.DisconnectAsync());
            while (client1.IsConnected || client1.IsHandshaked || (server.Clients != 2))
                Thread.Yield();

            // Multicast some data to all clients
            server.Multicast("test");

            // Wait for all data processed...
            while ((client1.BytesReceived != 12) || (client2.BytesReceived != 12) || (client3.BytesReceived != 8))
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client2.DisconnectAsync());
            while (client2.IsConnected || client2.IsHandshaked || (server.Clients != 1))
                Thread.Yield();

            // Multicast some data to all clients
            server.Multicast("test");

            // Wait for all data processed...
            while ((client1.BytesReceived != 12) || (client2.BytesReceived != 12) || (client3.BytesReceived != 12))
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client3.DisconnectAsync());
            while (client3.IsConnected || client3.IsHandshaked || (server.Clients != 0))
                Thread.Yield();

            // Stop the Echo server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the Echo server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.Connected);
            Assert.True(server.Handshaked);
            Assert.True(server.Disconnected);
            Assert.True(server.BytesSent > 0);
            Assert.True(server.BytesReceived == 0);
            Assert.True(!server.Errors);

            // Check the Echo client state
            Assert.True(client1.BytesSent == 0);
            Assert.True(client2.BytesSent == 0);
            Assert.True(client3.BytesSent == 0);
            Assert.True(client1.BytesReceived == 12);
            Assert.True(client2.BytesReceived == 12);
            Assert.True(client3.BytesReceived == 12);
            Assert.True(!client1.Errors);
            Assert.True(!client2.Errors);
            Assert.True(!client3.Errors);
        }

        [Fact(DisplayName = "SSL server random test")]
        public void SslServerRandomTest()
        {
            string address = "127.0.0.1";
            int port = 2224;

            // Create and prepare a new SSL server context
            var serverContext = EchoSslServer.CreateContext();

            // Create and start Echo server
            var server = new EchoSslServer(serverContext, IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Test duration in seconds
            int duration = 10;

            // Create and prepare a new SSL client context
            var clientContext = EchoSslClient.CreateContext();

            // Clients collection
            var clients = new List<EchoSslClient>();

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
                        // Create and connect Echo client
                        var client = new EchoSslClient(clientContext, address, port);
                        clients.Add(client);
                        client.ConnectAsync();
                        while (!client.IsHandshaked)
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
                        if (client.IsHandshaked)
                        {
                            client.DisconnectAsync();
                            while (client.IsConnected)
                                Thread.Yield();
                        }
                        else if (!client.IsConnected)
                        {
                            client.ConnectAsync();
                            while (!client.IsHandshaked)
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
                        if (client.IsHandshaked)
                        {
                            client.ReconnectAsync();
                            while (!client.IsHandshaked)
                                Thread.Yield();
                        }
                    }
                }
                // Multicast a message to all clients
                else if ((rand.Next() % 10) == 0)
                {
                    server.Multicast("test");
                }
                // Send a message from the random client
                else if ((rand.Next() % 1) == 0)
                {
                    if (clients.Count > 0)
                    {
                        int index = rand.Next() % clients.Count;
                        var client = clients[index];
                        if (client.IsHandshaked)
                            client.SendAsync("test");
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

            // Stop the Echo server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the Echo server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.Connected);
            Assert.True(server.Handshaked);
            Assert.True(server.Disconnected);
            Assert.True(server.BytesSent > 0);
            Assert.True(server.BytesReceived > 0);
            Assert.True(!server.Errors);
        }
    }
}
