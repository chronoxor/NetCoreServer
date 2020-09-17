# NetCoreServer

[![Linux build status](https://img.shields.io/travis/chronoxor/NetCoreServer/master.svg?label=Linux)](https://travis-ci.com/chronoxor/NetCoreServer)
[![OSX build status](https://img.shields.io/travis/chronoxor/NetCoreServer/master.svg?label=OSX)](https://travis-ci.com/chronoxor/NetCoreServer)
[![Windows build status](https://img.shields.io/appveyor/ci/chronoxor/NetCoreServer/master.svg?label=Windows)](https://ci.appveyor.com/project/chronoxor/NetCoreServer)
[![NuGet](https://img.shields.io/nuget/v/NetCoreServer.svg)](https://www.nuget.org/packages/NetCoreServer/)

Ultra fast and low latency asynchronous socket server & client C# .NET Core
library with support TCP, SSL, UDP, HTTP, HTTPS, WebSocket protocols and [10K connections problem](https://en.wikipedia.org/wiki/C10k_problem)
solution.

[NetCoreServer documentation](https://chronoxor.github.io/NetCoreServer)<br/>
[NetCoreServer downloads](https://github.com/chronoxor/NetCoreServer/releases)<br/>

# Contents
  * [Features](#features)
  * [Requirements](#requirements)
  * [How to build?](#how-to-build)
  * [Examples](#examples)
    * [Example: TCP chat server](#example-tcp-chat-server)
    * [Example: TCP chat client](#example-tcp-chat-client)
    * [Example: SSL chat server](#example-ssl-chat-server)
    * [Example: SSL chat client](#example-ssl-chat-client)
    * [Example: UDP echo server](#example-udp-echo-server)
    * [Example: UDP echo client](#example-udp-echo-client)
    * [Example: UDP multicast server](#example-udp-multicast-server)
    * [Example: UDP multicast client](#example-udp-multicast-client)
    * [Example: HTTP server](#example-http-server)
    * [Example: HTTP client](#example-http-client)
    * [Example: HTTPS server](#example-https-server)
    * [Example: HTTPS client](#example-https-client)
    * [Example: WebSocket chat server](#example-websocket-chat-server)
    * [Example: WebSocket chat client](#example-websocket-chat-client)
    * [Example: WebSocket secure chat server](#example-websocket-secure-chat-server)
    * [Example: WebSocket secure chat client](#example-websocket-secure-chat-client)
  * [Performance](#performance)
    * [Benchmark: Round-Trip](#benchmark-round-trip)
      * [TCP echo server](#tcp-echo-server)
      * [SSL echo server](#ssl-echo-server)
      * [UDP echo server](#udp-echo-server)
      * [WebSocket echo server](#websocket-echo-server)
      * [WebSocket secure echo server](#websocket-secure-echo-server)
    * [Benchmark: Multicast](#benchmark-multicast)
      * [TCP multicast server](#tcp-multicast-server)
      * [SSL multicast server](#ssl-multicast-server)
      * [UDP multicast server](#udp-multicast-server)
      * [WebSocket multicast server](#websocket-multicast-server)
      * [WebSocket secure multicast server](#websocket-secure-multicast-server)
    * [Benchmark: Web Server](#benchmark-web-server)
      * [HTTP Trace server](#http-trace-server)
      * [HTTPS Trace server](#https-trace-server)
  * [OpenSSL certificates](#openssl-certificates)
    * [Certificate Authority](#certificate-authority)
    * [SSL Server certificate](#ssl-server-certificate)
    * [SSL Client certificate](#ssl-client-certificate)
    * [Diffie-Hellman key exchange](#diffie-hellman-key-exchange)

# Features
* Cross platform (Linux, OSX, Windows)
* Asynchronous communication
* Supported transport protocols: [TCP](#example-tcp-chat-server), [SSL](#example-ssl-chat-server),
  [UDP](#example-udp-echo-server), [UDP multicast](#example-udp-multicast-server)
* Supported Web protocols: [HTTP](#example-http-server), [HTTPS](#example-https-server),
  [WebSocket](#example-websocket-chat-server), [WebSocket secure](#example-websocket-secure-chat-server)
* Supported [Swagger OpenAPI](https://swagger.io/specification/) iterative documentation

# Requirements
* Linux
* OSX
* Windows 10
* [.NET Core](https://dotnet.microsoft.com/download)
* [7-Zip](https://www.7-zip.org)
* [cmake](https://www.cmake.org)
* [git](https://git-scm.com)
* [Visual Studio](https://www.visualstudio.com)

Optional:
* [Rider](https://www.jetbrains.com/rider)

# How to build?

### Setup repository
```shell
git clone https://github.com/chronoxor/NetCoreServer.git
cd NetCoreServer
```

### Linux
```shell
cd build
./unix.sh
```

### OSX
```shell
cd build
./unix.sh
```

### Windows (Visual Studio)
Open and build [NetCoreServer.sln](https://github.com/chronoxor/NetCoreServer/blob/master/NetCoreServer.sln) or run the build script:
```shell
cd build
vs.bat
```

The build script will create "release" directory with zip files:
* NetCoreServer.zip - C# Server assembly
* Benchmarks.zip - C# Server benchmarks
* Examples.zip - C# Server examples

# Examples

## Example: TCP chat server
Here comes the example of the TCP chat server. It handles multiple TCP client
sessions and multicast received message from any session to all ones. Also it
is possible to send admin message directly from the server.

```c#
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace TcpChatServer
{
    class ChatSession : TcpSession
    {
        public ChatSession(TcpServer server) : base(server) {}

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";
            SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP session caught an error with code {error}");
        }
    }

    class ChatServer : TcpServer
    {
        public ChatServer(IPAddress address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new ChatSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // TCP server port
            int port = 1111;
            if (args.Length > 0)
                port = int.Parse(args[0]);

            Console.WriteLine($"TCP server port: {port}");

            Console.WriteLine();

            // Create a new TCP chat server
            var server = new ChatServer(IPAddress.Any, port);

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

                // Multicast admin message to all sessions
                line = "(admin) " + line;
                server.Multicast(line);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: TCP chat client
Here comes the example of the TCP chat client. It connects to the TCP chat
server and allows to send message to it and receive new messages.

```c#
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TcpClient = NetCoreServer.TcpClient;

namespace TcpChatClient
{
    class ChatClient : TcpClient
    {
        public ChatClient(string address, int port) : base(address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat TCP client connected a new session with Id {Id}");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat TCP client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // TCP server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // TCP server port
            int port = 1111;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"TCP server address: {address}");
            Console.WriteLine($"TCP server port: {port}");

            Console.WriteLine();

            // Create a new TCP chat client
            var client = new ChatClient(address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.ConnectAsync();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Disconnect the client
                if (line == "!")
                {
                    Console.Write("Client disconnecting...");
                    client.DisconnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Send the entered text to the chat server
                client.SendAsync(line);
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: SSL chat server
Here comes the example of the SSL chat server. It handles multiple SSL client
sessions and multicast received message from any session to all ones. Also it
is possible to send admin message directly from the server.

This example is very similar to the TCP one except the code that prepares SSL
context and handshake handler.

```c#
using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NetCoreServer;

namespace SslChatServer
{
    class ChatSession : SslSession
    {
        public ChatSession(SslServer server) : base(server) {}

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat SSL session with Id {Id} connected!");
        }

        protected override void OnHandshaked()
        {
            Console.WriteLine($"Chat SSL session with Id {Id} handshaked!");

            // Send invite message
            string message = "Hello from SSL chat! Please send a message or '!' to disconnect the client!";
            Send(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat SSL session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat SSL session caught an error with code {error}");
        }
    }

    class ChatServer : SslServer
    {
        public ChatServer(SslContext context, IPAddress address, int port) : base(context, address, port) {}

        protected override SslSession CreateSession() { return new ChatSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat SSL server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // SSL server port
            int port = 2222;
            if (args.Length > 0)
                port = int.Parse(args[0]);

            Console.WriteLine($"SSL server port: {port}");

            Console.WriteLine();

            // Create and prepare a new SSL server context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("server.pfx", "qwerty"));

            // Create a new SSL chat server
            var server = new ChatServer(context, IPAddress.Any, port);

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

                // Multicast admin message to all sessions
                line = "(admin) " + line;
                server.Multicast(line);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: SSL chat client
Here comes the example of the SSL chat client. It connects to the SSL chat
server and allows to send message to it and receive new messages.

This example is very similar to the TCP one except the code that prepares SSL
context and handshake handler.

```c#
using System;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using NetCoreServer;

namespace SslChatClient
{
    class ChatClient : SslClient
    {
        public ChatClient(SslContext context, string address, int port) : base(context, address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat SSL client connected a new session with Id {Id}");
        }

        protected override void OnHandshaked()
        {
            Console.WriteLine($"Chat SSL client handshaked a new session with Id {Id}");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat SSL client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat SSL client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // SSL server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // SSL server port
            int port = 2222;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"SSL server address: {address}");
            Console.WriteLine($"SSL server port: {port}");

            Console.WriteLine();

            // Create and prepare a new SSL client context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("client.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);

            // Create a new SSL chat client
            var client = new ChatClient(context, address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.ConnectAsync();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Disconnect the client
                if (line == "!")
                {
                    Console.Write("Client disconnecting...");
                    client.DisconnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Send the entered text to the chat server
                client.SendAsync(line);
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: UDP echo server
Here comes the example of the UDP echo server. It receives a datagram mesage
from any UDP client and resend it back without any changes.

```c#
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace UdpEchoServer
{
    class EchoServer : UdpServer
    {
        public EchoServer(IPAddress address, int port) : base(address, port) {}

        protected override void OnStarted()
        {
            // Start receive datagrams
            ReceiveAsync();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

            // Echo the message back to the sender
            SendAsync(endpoint, buffer, 0, size);
        }

        protected override void OnSent(EndPoint endpoint, long sent)
        {
            // Continue receive datagrams
            ReceiveAsync();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Echo UDP server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // UDP server port
            int port = 3333;
            if (args.Length > 0)
                port = int.Parse(args[0]);

            Console.WriteLine($"UDP server port: {port}");

            Console.WriteLine();

            // Create a new UDP echo server
            var server = new EchoServer(IPAddress.Any, port);

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
```

## Example: UDP echo client
Here comes the example of the UDP echo client. It sends user datagram message
to UDP server and listen for response.

```c#
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UdpClient = NetCoreServer.UdpClient;

namespace UdpEchoClient
{
    class EchoClient : UdpClient
    {
        public EchoClient(string address, int port) : base(address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            Disconnect();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Echo UDP client connected a new session with Id {Id}");

            // Start receive datagrams
            ReceiveAsync();
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Echo UDP client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                Connect();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

            // Continue receive datagrams
            ReceiveAsync();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Echo UDP client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // UDP server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // UDP server port
            int port = 3333;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"UDP server address: {address}");
            Console.WriteLine($"UDP server port: {port}");

            Console.WriteLine();

            // Create a new TCP chat client
            var client = new EchoClient(address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.Connect();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Disconnect the client
                if (line == "!")
                {
                    Console.Write("Client disconnecting...");
                    client.Disconnect();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Send the entered text to the chat server
                client.Send(line);
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: UDP multicast server
Here comes the example of the UDP multicast server. It use multicast IP address
to multicast datagram messages to all client that joined corresponding UDP
multicast group.

```c#
using System;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;

namespace UdpMulticastServer
{
    class MulticastServer : UdpServer
    {
        public MulticastServer(IPAddress address, int port) : base(address, port) {}

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Multicast UDP server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // UDP multicast address
            string multicastAddress = "239.255.0.1";
            if (args.Length > 0)
                multicastAddress = args[0];

            // UDP multicast port
            int multicastPort = 3334;
            if (args.Length > 1)
                multicastPort = int.Parse(args[1]);

            Console.WriteLine($"UDP multicast address: {multicastAddress}");
            Console.WriteLine($"UDP multicast port: {multicastPort}");

            Console.WriteLine();

            // Create a new UDP multicast server
            var server = new MulticastServer(IPAddress.Any, 0);

            // Start the multicast server
            Console.Write("Server starting...");
            server.Start(multicastAddress, multicastPort);
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

                // Multicast admin message to all sessions
                line = "(admin) " + line;
                server.Multicast(line);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: UDP multicast client
Here comes the example of the UDP multicast client. It use multicast IP address
and joins UDP multicast group in order to receive multicasted datagram messages
from UDP server.

```c#
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UdpClient = NetCoreServer.UdpClient;

namespace UdpMulticastClient
{
    class MulticastClient : UdpClient
    {
        public string Multicast;

        public MulticastClient(string address, int port) : base(address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            Disconnect();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Multicast UDP client connected a new session with Id {Id}");

            // Join UDP multicast group
            JoinMulticastGroup(Multicast);

            // Start receive datagrams
            ReceiveAsync();
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Multicast UDP client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                Connect();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

            // Continue receive datagrams
            ReceiveAsync();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Multicast UDP client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // UDP listen address
            string listenAddress = "0.0.0.0";
            if (args.Length > 0)
                listenAddress = args[0];

            // UDP multicast address
            string multicastAddress = "239.255.0.1";
            if (args.Length > 1)
                multicastAddress = args[1];

            // UDP multicast port
            int multicastPort = 3334;
            if (args.Length > 2)
                multicastPort = int.Parse(args[2]);

            Console.WriteLine($"UDP listen address: {listenAddress}");
            Console.WriteLine($"UDP multicast address: {multicastAddress}");
            Console.WriteLine($"UDP multicast port: {multicastPort}");

            Console.WriteLine();

            // Create a new TCP chat client
            var client = new MulticastClient(listenAddress, multicastPort);
            client.SetupMulticast(true);
            client.Multicast = multicastAddress;

            // Connect the client
            Console.Write("Client connecting...");
            client.Connect();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Disconnect the client
                if (line == "!")
                {
                    Console.Write("Client disconnecting...");
                    client.Disconnect();
                    Console.WriteLine("Done!");
                    continue;
                }
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: HTTP server
Here comes the example of the HTTP cache server. It allows to manipulate
cache data with HTTP methods (GET, POST, PUT and DELETE).

Use the following link to open [Swagger OpenAPI](https://swagger.io/specification/) iterative documentation: http://localhost:8080/api/index.html

![OpenAPI-HTTP](https://github.com/chronoxor/NetCoreServer/raw/master/images/openapi-http.png)

```c#
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace HttpServer
{
    class CommonCache
    {
        public static CommonCache GetInstance()
        {
            if (_instance == null)
                _instance = new CommonCache();
            return _instance;
        }

        public string GetAllCache()
        {
            var result = new StringBuilder();
            result.Append("[\n");
            foreach (var item in _cache)
            {
                result.Append("  {\n");
                result.AppendFormat($"    \"key\": \"{item.Key}\",\n");
                result.AppendFormat($"    \"value\": \"{item.Value}\",\n");
                result.Append("  },\n");
            }
            result.Append("]\n");
            return result.ToString();
        }

        public bool GetCacheValue(string key, out string value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void PutCacheValue(string key, string value)
        {
            _cache[key] = value;
        }

        public bool DeleteCacheValue(string key, out string value)
        {
            return _cache.TryRemove(key, out value);
        }

        private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();
        private static CommonCache _instance;
    }

    class HttpCacheSession : HttpSession
    {
        public HttpCacheSession(NetCoreServer.HttpServer server) : base(server) {}

        protected override void OnReceivedRequest(HttpRequest request)
        {
            // Show HTTP request content
            Console.WriteLine(request);

            // Process HTTP request methods
            if (request.Method == "HEAD")
                SendResponseAsync(Response.MakeHeadResponse());
            else if (request.Method == "GET")
            {
                string key = request.Url;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                if (string.IsNullOrEmpty(key))
                {
                    // Response with all cache values
                    SendResponseAsync(Response.MakeGetResponse(CommonCache.GetInstance().GetAllCache(), "application/json; charset=UTF-8"));
                }
                // Get the cache value by the given key
                else if (CommonCache.GetInstance().GetCacheValue(key, out var value))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(value));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Required cache value was not found for the key: " + key, 404));
            }
            else if ((request.Method == "POST") || (request.Method == "PUT"))
            {
                string key = request.Url;
                string value = request.Body;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                // Put the cache value
                CommonCache.GetInstance().PutCacheValue(key, value);

                // Response with the cache value
                SendResponseAsync(Response.MakeOkResponse());
            }
            else if (request.Method == "DELETE")
            {
                string key = request.Url;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                // Delete the cache value
                if (CommonCache.GetInstance().DeleteCacheValue(key, out var value))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(value));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Deleted cache value was not found for the key: " + key, 404));
            }
            else if (request.Method == "OPTIONS")
                SendResponseAsync(Response.MakeOptionsResponse());
            else if (request.Method == "TRACE")
                SendResponseAsync(Response.MakeTraceResponse(request.Cache.Data));
            else
                SendResponseAsync(Response.MakeErrorResponse("Unsupported HTTP method: " + request.Method));
        }

        protected override void OnReceivedRequestError(HttpRequest request, string error)
        {
            Console.WriteLine($"Request error: {error}");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTP session caught an error: {error}");
        }
    }

    class HttpCacheServer : NetCoreServer.HttpServer
    {
        public HttpCacheServer(IPAddress address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new HttpCacheSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTP session caught an error: {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // HTTP server port
            int port = 8080;
            if (args.Length > 0)
                port = int.Parse(args[0]);
            // HTTP server content path
            string www = "../../../../../www/api";
            if (args.Length > 1)
                www = args[1];

            Console.WriteLine($"HTTP server port: {port}");
            Console.WriteLine($"HTTP server static content path: {www}");
            Console.WriteLine($"HTTP server website: http://localhost:{port}/api/index.html");

            Console.WriteLine();

            // Create a new HTTP server
            var server = new HttpCacheServer(IPAddress.Any, port);
            server.AddStaticContent(www, "/api");

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
```

## Example: HTTP client
Here comes the example of the HTTP client. It allows to send HTTP requests
(GET, POST, PUT and DELETE) and receive HTTP responses.

```c#
using System;
using NetCoreServer;

namespace HttpClient
{
    class Program
    {
        static void Main(string[] args)
        {
            // HTTP server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // HTTP server port
            int port = 8080;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"HTTP server address: {address}");
            Console.WriteLine($"HTTP server port: {port}");

            Console.WriteLine();

            // Create a new HTTP client
            var client = new HttpClientEx(address, port);

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Reconnect the client
                if (line == "!")
                {
                    Console.Write("Client reconnecting...");
                    if (client.IsConnected)
                        client.ReconnectAsync();
                    else
                        client.ConnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }

                var commands = line.Split(' ');
                if (commands.Length < 2)
                {
                    Console.WriteLine("HTTP method and URL must be entered!");
                    continue;
                }

                if (commands[0].ToUpper() == "HEAD")
                {
                    var response = client.SendHeadRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "GET")
                {
                    var response = client.SendGetRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "POST")
                {
                    if (commands.Length < 3)
                    {
                        Console.WriteLine("HTTP method, URL and body must be entered!");
                        continue;
                    }

                    var response = client.SendPostRequest(commands[1], commands[2]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "PUT")
                {
                    if (commands.Length < 3)
                    {
                        Console.WriteLine("HTTP method, URL and body must be entered!");
                        continue;
                    }

                    var response = client.SendPutRequest(commands[1], commands[2]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "DELETE")
                {
                    var response = client.SendDeleteRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "OPTIONS")
                {
                    var response = client.SendOptionsRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "TRACE")
                {
                    var response = client.SendTraceRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else
                    Console.WriteLine("Unknown HTTP method");
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.Disconnect();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: HTTPS server
Here comes the example of the HTTPS cache server. It allows to manipulate
cache data with HTTP methods (GET, POST, PUT and DELETE) with secured
transport protocol.

Use the following link to open [Swagger OpenAPI](https://swagger.io/specification/) iterative documentation: https://localhost:8443/api/index.html

![OpenAPI-HTTPS](https://github.com/chronoxor/NetCoreServer/raw/master/images/openapi-https.png)

```c#
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NetCoreServer;

namespace HttpsServer
{
    class CommonCache
    {
        public static CommonCache GetInstance()
        {
            if (_instance == null)
                _instance = new CommonCache();
            return _instance;
        }

        public string GetAllCache()
        {
            var result = new StringBuilder();
            result.Append("[\n");
            foreach (var item in _cache)
            {
                result.Append("  {\n");
                result.AppendFormat($"    \"key\": \"{item.Key}\",\n");
                result.AppendFormat($"    \"value\": \"{item.Value}\",\n");
                result.Append("  },\n");
            }
            result.Append("]\n");
            return result.ToString();
        }

        public bool GetCacheValue(string key, out string value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void PutCacheValue(string key, string value)
        {
            _cache[key] = value;
        }

        public bool DeleteCacheValue(string key, out string value)
        {
            return _cache.TryRemove(key, out value);
        }

        private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();
        private static CommonCache _instance;
    }

    class HttpsCacheSession : HttpsSession
    {
        public HttpsCacheSession(NetCoreServer.HttpsServer server) : base(server) { }

        protected override void OnReceivedRequest(HttpRequest request)
        {
            // Show HTTP request content
            Console.WriteLine(request);

            // Process HTTP request methods
            if (request.Method == "HEAD")
                SendResponseAsync(Response.MakeHeadResponse());
            else if (request.Method == "GET")
            {
                string key = request.Url;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                if (string.IsNullOrEmpty(key))
                {
                    // Response with all cache values
                    SendResponseAsync(Response.MakeGetResponse(CommonCache.GetInstance().GetAllCache(), "application/json; charset=UTF-8"));
                }
                // Get the cache value by the given key
                else if (CommonCache.GetInstance().GetCacheValue(key, out var value))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(value));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Required cache value was not found for the key: " + key, 404));
            }
            else if ((request.Method == "POST") || (request.Method == "PUT"))
            {
                string key = request.Url;
                string value = request.Body;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                // Put the cache value
                CommonCache.GetInstance().PutCacheValue(key, value);

                // Response with the cache value
                SendResponseAsync(Response.MakeOkResponse());
            }
            else if (request.Method == "DELETE")
            {
                string key = request.Url;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                // Delete the cache value
                if (CommonCache.GetInstance().DeleteCacheValue(key, out var value))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(value));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Deleted cache value was not found for the key: " + key, 404));
            }
            else if (request.Method == "OPTIONS")
                SendResponseAsync(Response.MakeOptionsResponse());
            else if (request.Method == "TRACE")
                SendResponseAsync(Response.MakeTraceResponse(request.Cache));
            else
                SendResponseAsync(Response.MakeErrorResponse("Unsupported HTTP method: " + request.Method));
        }

        protected override void OnReceivedRequestError(HttpRequest request, string error)
        {
            Console.WriteLine($"Request error: {error}");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTPS session caught an error: {error}");
        }
    }

    class HttpsCacheServer : NetCoreServer.HttpsServer
    {
        public HttpsCacheServer(SslContext context, IPAddress address, int port) : base(context, address, port) {}

        protected override SslSession CreateSession() { return new HttpsCacheSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTPS server caught an error: {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // HTTPS server port
            int port = 8443;
            if (args.Length > 0)
                port = int.Parse(args[0]);
            // HTTPS server content path
            string www = "../../../../../www/api";
            if (args.Length > 1)
                www = args[1];

            Console.WriteLine($"HTTPS server port: {port}");
            Console.WriteLine($"HTTPS server static content path: {www}");
            Console.WriteLine($"HTTPS server website: https://localhost:{port}/api/index.html");

            Console.WriteLine();

            // Create and prepare a new SSL server context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("server.pfx", "qwerty"));

            // Create a new HTTP server
            var server = new HttpsCacheServer(context, IPAddress.Any, port);
            server.AddStaticContent(www, "/api");

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
```

## Example: HTTPS client
Here comes the example of the HTTPS client. It allows to send HTTP requests
(GET, POST, PUT and DELETE) and receive HTTP responses with secured
transport protocol.

```c#
using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using NetCoreServer;

namespace HttpsClient
{
    class Program
    {
        static void Main(string[] args)
        {
            // HTTPS server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // HTTPS server port
            int port = 8443;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"HTTPS server address: {address}");
            Console.WriteLine($"HTTPS server port: {port}");

            Console.WriteLine();

            // Create and prepare a new SSL client context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("client.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);

            // Create a new HTTPS client
            var client = new HttpsClientEx(context, address, port);

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Reconnect the client
                if (line == "!")
                {
                    Console.Write("Client reconnecting...");
                    if (client.IsConnected)
                        client.ReconnectAsync();
                    else
                        client.ConnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }

                var commands = line.Split(' ');
                if (commands.Length < 2)
                {
                    Console.WriteLine("HTTP method and URL must be entered!");
                    continue;
                }

                if (commands[0].ToUpper() == "HEAD")
                {
                    var response = client.SendHeadRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "GET")
                {
                    var response = client.SendGetRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "POST")
                {
                    if (commands.Length < 3)
                    {
                        Console.WriteLine("HTTP method, URL and body must be entered!");
                        continue;
                    }

                    var response = client.SendPostRequest(commands[1], commands[2]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "PUT")
                {
                    if (commands.Length < 3)
                    {
                        Console.WriteLine("HTTP method, URL and body must be entered!");
                        continue;
                    }

                    var response = client.SendPutRequest(commands[1], commands[2]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "DELETE")
                {
                    var response = client.SendDeleteRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "OPTIONS")
                {
                    var response = client.SendOptionsRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else if (commands[0].ToUpper() == "TRACE")
                {
                    var response = client.SendTraceRequest(commands[1]).Result;
                    Console.WriteLine(response);
                }
                else
                    Console.WriteLine("Unknown HTTP method");
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.Disconnect();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: WebSocket chat server
Here comes the example of the WebSocket chat server. It handles multiple
WebSocket client sessions and multicast received message from any session
to all ones. Also it is possible to send admin message directly from the
server.

Use the following link to open WebSocket chat server example: http://localhost:8080/chat/index.html

![ws-chat](https://github.com/chronoxor/NetCoreServer/raw/master/images/ws-chat.png)

```c#
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace WsChatServer
{
    class ChatSession : WsSession
    {
        public ChatSession(WsServer server) : base(server) { }

        public override void OnWsConnected(HttpRequest request)
        {
            Console.WriteLine($"Chat WebSocket session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from WebSocket chat! Please send a message or '!' to disconnect the client!";
            SendTextAsync(message);
        }

        public override void OnWsDisconnected()
        {
            Console.WriteLine($"Chat WebSocket session with Id {Id} disconnected!");
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            ((WsServer)Server).MulticastText(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Close(1000);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket session caught an error with code {error}");
        }
    }

    class ChatServer : WsServer
    {
        public ChatServer(IPAddress address, int port) : base(address, port) { }

        protected override TcpSession CreateSession() { return new ChatSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // WebSocket server port
            int port = 8080;
            if (args.Length > 0)
                port = int.Parse(args[0]);
            // WebSocket server content path
            string www = "../../../../../www/ws";
            if (args.Length > 1)
                www = args[1];

            Console.WriteLine($"WebSocket server port: {port}");
            Console.WriteLine($"WebSocket server static content path: {www}");
            Console.WriteLine($"WebSocket server website: http://localhost:{port}/chat/index.html");

            Console.WriteLine();

            // Create a new WebSocket server
            var server = new ChatServer(IPAddress.Any, port);
            server.AddStaticContent(www, "/chat");

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

                // Multicast admin message to all sessions
                line = "(admin) " + line;
                server.MulticastText(line);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: WebSocket chat client
Here comes the example of the WebSocket chat client. It connects to the
WebSocket chat server and allows to send message to it and receive new
messages.

```c#
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetCoreServer;

namespace WsChatClient
{
    class ChatClient : WsClient
    {
        public ChatClient(string address, int port) : base(address, port) { }

        public void DisconnectAndStop()
        {
            _stop = true;
            CloseAsync(1000);
            while (IsConnected)
                Thread.Yield();
        }

        public override void OnWsConnecting(HttpRequest request)
        {
            request.SetBegin("GET", "/");
            request.SetHeader("Host", "localhost");
            request.SetHeader("Origin", "http://localhost");
            request.SetHeader("Upgrade", "websocket");
            request.SetHeader("Connection", "Upgrade");
            request.SetHeader("Sec-WebSocket-Key", Convert.ToBase64String(Encoding.UTF8.GetBytes(Id.ToString())));
            request.SetHeader("Sec-WebSocket-Protocol", "chat, superchat");
            request.SetHeader("Sec-WebSocket-Version", "13");
        }

        public override void OnWsConnected(HttpResponse response)
        {
            Console.WriteLine($"Chat WebSocket client connected a new session with Id {Id}");
        }

        public override void OnWsDisconnected()
        {
            Console.WriteLine($"Chat WebSocket client disconnected a session with Id {Id}");
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine($"Incoming: {Encoding.UTF8.GetString(buffer, (int)offset, (int)size)}");
        }

        protected override void OnDisconnected()
        {
            base.OnDisconnected();

            Console.WriteLine($"Chat WebSocket client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // WebSocket server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // WebSocket server port
            int port = 8080;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"WebSocket server address: {address}");
            Console.WriteLine($"WebSocket server port: {port}");

            Console.WriteLine();

            // Create a new TCP chat client
            var client = new ChatClient(address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.ConnectAsync();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Disconnect the client
                if (line == "!")
                {
                    Console.Write("Client disconnecting...");
                    client.DisconnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Send the entered text to the chat server
                client.SendTextAsync(line);
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: WebSocket secure chat server
Here comes the example of the WebSocket secure chat server. It handles
multiple WebSocket secure client sessions and multicast received message
from any session to all ones. Also it is possible to send admin message
directly from the server.

This example is very similar to the WebSocket one except the code that
prepares WebSocket secure context and handshake handler.

Use the following link to open WebSocket secure chat server example: https://localhost:8443/chat/index.html

![wss-chat](https://github.com/chronoxor/NetCoreServer/raw/master/images/wss-chat.png)

```c#
using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NetCoreServer;

namespace WssChatServer
{
    class ChatSession : WssSession
    {
        public ChatSession(WssServer server) : base(server) { }

        public override void OnWsConnected(HttpRequest request)
        {
            Console.WriteLine($"Chat WebSocket session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from WebSocket chat! Please send a message or '!' to disconnect the client!";
            SendTextAsync(message);
        }

        public override void OnWsDisconnected()
        {
            Console.WriteLine($"Chat WebSocket session with Id {Id} disconnected!");
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            ((WssServer)Server).MulticastText(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Close(1000);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket session caught an error with code {error}");
        }
    }

    class ChatServer : WssServer
    {
        public ChatServer(SslContext context, IPAddress address, int port) : base(context, address, port) { }

        protected override SslSession CreateSession() { return new ChatSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // WebSocket server port
            int port = 8443;
            if (args.Length > 0)
                port = int.Parse(args[0]);
            // WebSocket server content path
            string www = "../../../../../www/wss";
            if (args.Length > 1)
                www = args[1];

            Console.WriteLine($"WebSocket server port: {port}");
            Console.WriteLine($"WebSocket server static content path: {www}");
            Console.WriteLine($"WebSocket server website: https://localhost:{port}/chat/index.html");

            Console.WriteLine();

            // Create and prepare a new SSL server context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("server.pfx", "qwerty"));

            // Create a new WebSocket server
            var server = new ChatServer(context, IPAddress.Any, port);
            server.AddStaticContent(www, "/chat");

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

                // Multicast admin message to all sessions
                line = "(admin) " + line;
                server.MulticastText(line);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
```

## Example: WebSocket secure chat client
Here comes the example of the WebSocket secure chat client. It connects to
the WebSocket secure chat server and allows to send message to it and receive
new messages.

This example is very similar to the WebSocket one except the code that
prepares WebSocket secure context and handshake handler.

```c#
using System;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using NetCoreServer;

namespace WssChatClient
{
    class ChatClient : WssClient
    {
        public ChatClient(SslContext context, string address, int port) : base(context, address, port) { }

        public void DisconnectAndStop()
        {
            _stop = true;
            CloseAsync(1000);
            while (IsConnected)
                Thread.Yield();
        }

        public override void OnWsConnecting(HttpRequest request)
        {
            request.SetBegin("GET", "/");
            request.SetHeader("Host", "localhost");
            request.SetHeader("Origin", "http://localhost");
            request.SetHeader("Upgrade", "websocket");
            request.SetHeader("Connection", "Upgrade");
            request.SetHeader("Sec-WebSocket-Key", Convert.ToBase64String(Encoding.UTF8.GetBytes(Id.ToString())));
            request.SetHeader("Sec-WebSocket-Protocol", "chat, superchat");
            request.SetHeader("Sec-WebSocket-Version", "13");
        }

        public override void OnWsConnected(HttpResponse response)
        {
            Console.WriteLine($"Chat WebSocket client connected a new session with Id {Id}");
        }

        public override void OnWsDisconnected()
        {
            Console.WriteLine($"Chat WebSocket client disconnected a session with Id {Id}");
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine($"Incoming: {Encoding.UTF8.GetString(buffer, (int)offset, (int)size)}");
        }

        protected override void OnDisconnected()
        {
            base.OnDisconnected();

            Console.WriteLine($"Chat WebSocket client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket client caught an error with code {error}");
        }

        private bool _stop;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // WebSocket server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // WebSocket server port
            int port = 8443;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"WebSocket server address: {address}");
            Console.WriteLine($"WebSocket server port: {port}");

            Console.WriteLine();

            // Create and prepare a new SSL client context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("client.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);

            // Create a new TCP chat client
            var client = new ChatClient(context, address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.ConnectAsync();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Reconnect the client
                if (line == "!")
                {
                    Console.Write("Client reconnecting...");
                    if (client.IsConnected)
                        client.ReconnectAsync();
                    else
                        client.ConnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Send the entered text to the chat server
                client.SendTextAsync(line);
            }

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
```

# Performance

Here comes several communication scenarios with timing measurements.

Benchmark environment is the following:
```
CPU architecutre: Intel(R) Core(TM) i7-4790K CPU @ 4.00GHz
CPU logical cores: 8
CPU physical cores: 4
CPU clock speed: 3.998 GHz
CPU Hyper-Threading: enabled
RAM total: 31.962 GiB
RAM free: 24.011 GiB

OS version: Microsoft Windows 8 Enterprise Edition (build 9200), 64-bit
OS bits: 64-bit
Process bits: 64-bit
Process configuaraion: release
```

## Benchmark: Round-Trip

![Round-trip](https://github.com/chronoxor/NetCoreServer/raw/master/images/round-trip.png)

This scenario sends lots of messages from several clients to a server.
The server responses to each message and resend the similar response to
the client. The benchmark measures total Round-trip time to send all
messages and receive all responses, messages & data throughput, count
of errors.

### TCP echo server

* [TcpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoServer/Program.cs)
* [TcpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 1111
Working clients: 1
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.024 s
Total data: 2.831 GiB
Total messages: 94369133
Data throughput: 287.299 MiB/s
Message latency: 106 ns
Message throughput: 9413997 msg/s
```

* [TcpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoServer/Program.cs)
* [TcpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 1111
Working clients: 100
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.189 s
Total data: 1.794 GiB
Total messages: 59585544
Data throughput: 178.463 MiB/s
Message latency: 171 ns
Message throughput: 5847523 msg/s
```

### SSL echo server

* [SslEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoServer/Program.cs)
* [SslEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 1
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 2.645 s
Total data: 373.329 MiB
Total messages: 12233021
Data throughput: 141.095 MiB/s
Message latency: 216 ns
Message throughput: 4623352 msg/s
```

* [SslEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoServer/Program.cs)
* [SslEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 100
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.060 s
Total data: 1.472 GiB
Total messages: 49029133
Data throughput: 148.741 MiB/s
Message latency: 205 ns
Message throughput: 4873398 msg/s
```

### UDP echo server

* [UdpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoServer/Program.cs)
* [UdpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 3333
Working clients: 1
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.032 s
Total data: 33.994 MiB
Total messages: 1113182
Data throughput: 3.395 MiB/s
Message latency: 9.012 mcs
Message throughput: 110960 msg/s
```

* [UdpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoServer/Program.cs)
* [UdpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 3333
Working clients: 100
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.635 s
Total data: 20.355 MiB
Total messages: 666791
Data throughput: 1.934 MiB/s
Message latency: 15.950 mcs
Message throughput: 62693 msg/s
```

### WebSocket echo server

* [WsEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WsEchoServer/Program.cs)
* [WsEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WsEchoClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 8080
Working clients: 1
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 3.037 s
Total data: 105.499 MiB
Total messages: 3456618
Data throughput: 34.742 MiB/s
Message latency: 878 ns
Message throughput: 1137864 msg/s
```

* [WsEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WsEchoServer/Program.cs)
* [WsEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WsEchoClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 8080
Working clients: 100
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.078 s
Total data: 426.803 MiB
Total messages: 13984888
Data throughput: 42.353 MiB/s
Message latency: 720 ns
Message throughput: 1387555 msg/s
```

### WebSocket secure echo server

* [WssEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WssEchoServer/Program.cs)
* [WssEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WssEchoClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 8443
Working clients: 1
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.030 s
Total data: 198.103 MiB
Total messages: 6491390
Data throughput: 19.767 MiB/s
Message latency: 1.545 mcs
Message throughput: 647153 msg/s
```

* [WssEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WssEchoServer/Program.cs)
* [WssEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WssEchoClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 8443
Working clients: 100
Working messages: 1000
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.112 s
Total data: 405.286 MiB
Total messages: 13280221
Data throughput: 40.078 MiB/s
Message latency: 761 ns
Message throughput: 1313228 msg/s
```

## Benchmark: Multicast

![Multicast](https://github.com/chronoxor/NetCoreServer/raw/master/images/multicast.png)

In this scenario server multicasts messages to all connected clients.
The benchmark counts total messages received by all clients for all
the working time and measures messages & data throughput, count
of errors.

### TCP multicast server

* [TcpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastServer/Program.cs)
* [TcpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 1111
Working clients: 1
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.022 s
Total data: 407.023 MiB
Total messages: 13337326
Data throughput: 40.625 MiB/s
Message latency: 751 ns
Message throughput: 1330734 msg/s
```

* [TcpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastServer/Program.cs)
* [TcpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 1111
Working clients: 100
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.112 s
Total data: 421.348 MiB
Total messages: 13806493
Data throughput: 41.681 MiB/s
Message latency: 732 ns
Message throughput: 1365280 msg/s
```

### SSL multicast server

* [SslMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastServer/Program.cs)
* [SslMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 1
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.024 s
Total data: 325.225 MiB
Total messages: 10656801
Data throughput: 32.453 MiB/s
Message latency: 940 ns
Message throughput: 1063075 msg/s
```

* [SslMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastServer/Program.cs)
* [SslMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 100
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.144 s
Total data: 343.460 MiB
Total messages: 11254173
Data throughput: 33.876 MiB/s
Message latency: 901 ns
Message throughput: 1109393 msg/s
```

### UDP multicast server

* [UdpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastServer/Program.cs)
* [UdpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastClient/Program.cs) -c 1

```
Server address: 239.255.0.1
Server port: 3333
Working clients: 1
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.026 s
Total data: 13.225 MiB
Total messages: 433202
Data throughput: 1.326 MiB/s
Message latency: 23.145 mcs
Message throughput: 43205 msg/s
```

* [UdpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastServer/Program.cs)
* [UdpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastClient/Program.cs) -c 100

```
Server address: 239.255.0.1
Server port: 3333
Working clients: 100
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.035 s
Total data: 28.684 MiB
Total messages: 939408
Data throughput: 2.877 MiB/s
Message latency: 10.682 mcs
Message throughput: 93606 msg/s
```

### WebSocket multicast server

* [WsMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WsMulticastServer/Program.cs)
* [WsMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WsMulticastClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 8080
Working clients: 1
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.048 s
Total data: 183.108 MiB
Total messages: 6000000
Data throughput: 18.228 MiB/s
Message latency: 1.674 mcs
Message throughput: 597121 msg/s
```

* [WsMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WsMulticastServer/Program.cs)
* [WsMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WsMulticastClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 8080
Working clients: 100
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.227 s
Total data: 125.957 MiB
Total messages: 4126627
Data throughput: 12.320 MiB/s
Message latency: 2.478 mcs
Message throughput: 403466 msg/s
```

### WebSocket secure multicast server

* [WssMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WssMulticastServer/Program.cs)
* [WssMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WssMulticastClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 8443
Working clients: 1
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.034 s
Total data: 184.159 MiB
Total messages: 6034421
Data throughput: 18.359 MiB/s
Message latency: 1.662 mcs
Message throughput: 601338 msg/s
```

* [WssMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WssMulticastServer/Program.cs)
* [WssMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/WssMulticastClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 8443
Working clients: 100
Message size: 32
Seconds to benchmarking: 10

Errors: 0

Total time: 10.171 s
Total data: 315.306 MiB
Total messages: 10331721
Data throughput: 30.1022 MiB/s
Message latency: 984 ns
Message throughput: 1015763 msg/s
```

## Benchmark: Web Server

### HTTP Trace server

* [HttpTraceServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/HttpTraceServer/Program.cs)
* [HttpTraceClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/HttpTraceClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 8080
Working clients: 1
Working messages: 1
Seconds to benchmarking: 10

Errors: 0

Total time: 10.023 s
Total data: 10.987 MiB
Total messages: 108465
Data throughput: 1.096 MiB/s
Message latency: 92.414 mcs
Message throughput: 10820 msg/s
```

* [HttpTraceServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/HttpTraceServer/Program.cs)
* [HttpTraceClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/HttpTraceClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 8080
Working clients: 100
Working messages: 1
Seconds to benchmarking: 10

Errors: 0

Total time: 10.085 s
Total data: 40.382 MiB
Total messages: 401472
Data throughput: 4.003 MiB/s
Message latency: 25.120 mcs
Message throughput: 39807 msg/s
```

### HTTPS Trace server

* [HttpsTraceServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/HttpsTraceServer/Program.cs)
* [HttpsTraceClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/HttpsTraceClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 8443
Working clients: 1
Working messages: 1
Seconds to benchmarking: 10

Errors: 0

Total time: 595.214 ms
Total data: 627.842 KiB
Total messages: 6065
Data throughput: 1.030 MiB/s
Message latency: 98.139 mcs
Message throughput: 10189 msg/s
```

* [HttpsTraceServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/HttpsTraceServer/Program.cs)
* [HttpsTraceClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/HttpsTraceClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 8443
Working clients: 100
Working messages: 1
Seconds to benchmarking: 10

Errors: 0

Total time: 3.548 s
Total data: 17.948 MiB
Total messages: 179111
Data throughput: 5.052 MiB/s
Message latency: 19.813 mcs
Message throughput: 50471 msg/s
```

# OpenSSL certificates
In order to create OpenSSL based server and client you should prepare a set of
SSL certificates. Here comes several steps to get a self-signed set of SSL
certificates for testing purposes:

## Certificate Authority

* Create CA private key
```shell
openssl genrsa -passout pass:qwerty -out ca-secret.key 4096
```

* Remove passphrase
```shell
openssl rsa -passin pass:qwerty -in ca-secret.key -out ca.key
```

* Create CA self-signed certificate
```shell
openssl req -new -x509 -days 3650 -subj '/C=BY/ST=Belarus/L=Minsk/O=Example root CA/OU=Example CA unit/CN=example.com' -key ca.key -out ca.crt
```

* Convert CA self-signed certificate to PFX
```shell
openssl pkcs12 -export -passout pass:qwerty -inkey ca.key -in ca.crt -out ca.pfx
```

* Convert CA self-signed certificate to PEM
```shell
openssl pkcs12 -passin pass:qwerty -passout pass:qwerty -in ca.pfx -out ca.pem
```

## SSL Server certificate

* Create private key for the server
```shell
openssl genrsa -passout pass:qwerty -out server-secret.key 4096
```

* Remove passphrase
```shell
openssl rsa -passin pass:qwerty -in server-secret.key -out server.key
```

* Create CSR for the server
```shell
openssl req -new -subj '/C=BY/ST=Belarus/L=Minsk/O=Example server/OU=Example server unit/CN=server.example.com' -key server.key -out server.csr
```

* Create certificate for the server
```shell
openssl x509 -req -days 3650 -in server.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out server.crt
```

* Convert the server certificate to PFX
```shell
openssl pkcs12 -export -passout pass:qwerty -inkey server.key -in server.crt -out server.pfx
```

* Convert the server certificate to PEM
```shell
openssl pkcs12 -passin pass:qwerty -passout pass:qwerty -in server.pfx -out server.pem
```

## SSL Client certificate

* Create private key for the client
```shell
openssl genrsa -passout pass:qwerty -out client-secret.key 4096
```

* Remove passphrase
```shell
openssl rsa -passin pass:qwerty -in client-secret.key -out client.key
```

* Create CSR for the client
```shell
openssl req -new -subj '/C=BY/ST=Belarus/L=Minsk/O=Example client/OU=Example client unit/CN=client.example.com' -key client.key -out client.csr
```

* Create the client certificate
```shell
openssl x509 -req -days 3650 -in client.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out client.crt
```

* Convert the client certificate to PFX
```shell
openssl pkcs12 -export -passout pass:qwerty -inkey client.key -in client.crt -out client.pfx
```

* Convert the client certificate to PEM
```shell
openssl pkcs12 -passin pass:qwerty -passout pass:qwerty -in client.pfx -out client.pem
```

## Diffie-Hellman key exchange

* Create DH parameters
```shell
openssl dhparam -out dh4096.pem 4096
```
