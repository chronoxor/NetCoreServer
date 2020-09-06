using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;
using Xunit;

namespace tests
{
    class EchoUdpClient : NetCoreServer.UdpClient
    {
        public bool Connected { get; set; }
        public bool Disconnected { get; set; }
        public bool Errors { get; set; }

        public EchoUdpClient(string address, int port) : base(address, port) {}

        protected override void OnConnected() { Connected = true; ReceiveAsync(); }
        protected override void OnDisconnected() { Disconnected = true; }
        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size) { ReceiveAsync(); }
        protected override void OnError(SocketError error) { Errors = true; }
    }

    class EchoUdpServer : UdpServer
    {
        public bool Started { get; set; }
        public bool Stopped { get; set; }
        public bool Errors { get; set; }

        public EchoUdpServer(IPAddress address, int port) : base(address, port) {}

        protected override void OnStarted() { Started = true; ReceiveAsync(); }
        protected override void OnStopped() { Stopped = true; }
        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size) { SendAsync(endpoint, buffer, offset, size); }
        protected override void OnSent(EndPoint endpoint, long sent) { ThreadPool.QueueUserWorkItem(o => { ReceiveAsync(); } ); }
        protected override void OnError(SocketError error) { Errors = true; }
    }

    public class UdpTests
    {
        [Fact(DisplayName = "UDP server test")]
        public void UdpServerTest()
        {
            string address = "127.0.0.1";
            int port = 3333;

            // Create and start Echo server
            var server = new EchoUdpServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create and connect Echo client
            var client = new EchoUdpClient(address, port);
            Assert.True(client.Connect());
            while (!client.IsConnected)
                Thread.Yield();

            // Send a message to the Echo server
            client.Send("test");

            // Wait for all data processed...
            while (client.BytesReceived != 4)
                Thread.Yield();

            // Disconnect the Echo client
            Assert.True(client.Disconnect());
            while (client.IsConnected)
                Thread.Yield();

            // Stop the Echo server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();

            // Check the Echo server state
            Assert.True(server.Started);
            Assert.True(server.Stopped);
            Assert.True(server.BytesSent == 4);
            Assert.True(server.BytesReceived == 4);
            Assert.True(!server.Errors);

            // Check the Echo client state
            Assert.True(client.Connected);
            Assert.True(client.Disconnected);
            Assert.True(client.BytesSent == 4);
            Assert.True(client.BytesReceived == 4);
            Assert.True(!client.Errors);
        }

        [Fact(DisplayName = "UDP server random test")]
        public void UdpServerRandomTest()
        {
            string address = "127.0.0.1";
            int port = 3334;

            // Create and start Echo server
            var server = new EchoUdpServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Test duration in seconds
            int duration = 10;

            // Clients collection
            var clients = new List<EchoUdpClient>();

            // Start random test
            var rand = new Random();
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < duration)
            {
                // Create a new client and connect
                if ((rand.Next() % 100) == 0)
                {
                    if (clients.Count < 100)
                    {
                        // Create and connect Echo client
                        var client = new EchoUdpClient(address, port);
                        clients.Add(client);
                        client.Connect();
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
                            client.Disconnect();
                            while (client.IsConnected)
                                Thread.Yield();
                        }
                        else
                        {
                            client.Connect();
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
                            client.Reconnect();
                            while (!client.IsConnected)
                                Thread.Yield();
                        }
                    }
                }
                // Send a message from the random client
                else if ((rand.Next() % 1) == 0)
                {
                    if (clients.Count > 0)
                    {
                        int index = rand.Next() % clients.Count;
                        var client = clients[index];
                        if (client.IsConnected)
                            client.Send("test");
                    }
                }

                // Sleep for a while...
                Thread.Sleep(1);
            }

            // Disconnect clients
            foreach (var client in clients)
            {
                client.Disconnect();
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
            Assert.True(server.BytesSent > 0);
            Assert.True(server.BytesReceived > 0);
            Assert.True(!server.Errors);
        }
    }
}
