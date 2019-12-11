# NetCoreServer

[![Linux build status](https://img.shields.io/travis/chronoxor/NetCoreServer/master.svg?label=Linux)](https://travis-ci.org/chronoxor/NetCoreServer)
[![OSX build status](https://img.shields.io/travis/chronoxor/NetCoreServer/master.svg?label=OSX)](https://travis-ci.org/chronoxor/NetCoreServer)
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
  * [Performance](#performance)
    * [Benchmark: Round-Trip](#benchmark-round-trip)
      * [TCP echo server](#tcp-echo-server)
      * [SSL echo server](#ssl-echo-server)
      * [UDP echo server](#udp-echo-server)
    * [Benchmark: Multicast](#benchmark-multicast)
      * [TCP multicast server](#tcp-multicast-server)
      * [SSL multicast server](#ssl-multicast-server)
      * [UDP multicast server](#udp-multicast-server)
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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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

        public bool GetCache(string key, out string value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out value))
                    return true;
                else
                    return false;
            }
        }

        public void SetCache(string key, string value)
        {
            lock (_cacheLock)
                _cache[key] = value;
        }

        public bool DeleteCache(string key, out string value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out value))
                {
                    _cache.Remove(key);
                    return true;
                }
                else
                    return false;
            }
        }

        private readonly object _cacheLock = new object();
        private SortedDictionary<string, string> _cache = new SortedDictionary<string, string>();
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
                // Get the cache value
                string cache;
                if (CommonCache.GetInstance().GetCache(request.Url, out cache))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(cache));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Required cache value was not found for the key: " + request.Url));
            }
            else if ((request.Method == "POST") || (request.Method == "PUT"))
            {
                // Set the cache value
                CommonCache.GetInstance().SetCache(request.Url, request.Body);
                // Response with the cache value
                SendResponseAsync(Response.MakeOkResponse());
            }
            else if (request.Method == "DELETE")
            {
                // Delete the cache value
                string cache;
                if (CommonCache.GetInstance().DeleteCache(request.Url, out cache))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(cache));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Deleted cache value was not found for the key: " + request.Url));
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
            Console.WriteLine($"HTTP session caught an error: {error.ToString()}");
        }
    }

    class HttpCacheServer : NetCoreServer.HttpServer
    {
        public HttpCacheServer(IPAddress address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new HttpCacheSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTP session caught an error: {error.ToString()}");
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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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

        public bool GetCache(string key, out string value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out value))
                    return true;
                else
                    return false;
            }
        }

        public void SetCache(string key, string value)
        {
            lock (_cacheLock)
                _cache[key] = value;
        }

        public bool DeleteCache(string key, out string value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out value))
                {
                    _cache.Remove(key);
                    return true;
                }
                else
                    return false;
            }
        }

        private readonly object _cacheLock = new object();
        private SortedDictionary<string, string> _cache = new SortedDictionary<string, string>();
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
                // Get the cache value
                string cache;
                if (CommonCache.GetInstance().GetCache(request.Url, out cache))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(cache));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Required cache value was not found for the key: " + request.Url));
            }
            else if ((request.Method == "POST") || (request.Method == "PUT"))
            {
                // Set the cache value
                CommonCache.GetInstance().SetCache(request.Url, request.Body);
                // Response with the cache value
                SendResponseAsync(Response.MakeOkResponse());
            }
            else if (request.Method == "DELETE")
            {
                // Delete the cache value
                string cache;
                if (CommonCache.GetInstance().DeleteCache(request.Url, out cache))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(cache));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Deleted cache value was not found for the key: " + request.Url));
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
            Console.WriteLine($"HTTPS session caught an error: {error.ToString()}");
        }
    }

    class HttpsCacheServer : NetCoreServer.HttpsServer
    {
        public HttpsCacheServer(SslContext context, IPAddress address, int port) : base(context, address, port) {}

        protected override SslSession CreateSession() { return new HttpsCacheSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTPS server caught an error: {error.ToString()}");
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
RAM free: 21.623 GiB

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

Total time: 10.017 s
Total data: 389.962 MiB
Total messages: 12777566
Data throughput: 38.948 MiB/s
Message latency: 783 ns
Message throughput: 1275543 msg/s
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

Total time: 10.179 s
Total data: 884.520 MiB
Total messages: 28983575
Data throughput: 86.911 MiB/s
Message latency: 351 ns
Message throughput: 2847229 msg/s
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

Total time: 10.088 s
Total data: 41.873 MiB
Total messages: 1371444
Data throughput: 4.152 MiB/s
Message latency: 7.356 mcs
Message throughput: 135939 msg/s
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

Total time: 12.270 s
Total data: 187.644 MiB
Total messages: 6148244
Data throughput: 15.298 MiB/s
Message latency: 1.995 mcs
Message throughput: 501056 msg/s
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

Total time: 10.023 s
Total data: 38.614 MiB
Total messages: 1264835
Data throughput: 3.871 MiB/s
Message latency: 7.924 mcs
Message throughput: 126187 msg/s
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

Total time: 10.465 s
Total data: 32.683 MiB
Total messages: 1070523
Data throughput: 3.124 MiB/s
Message latency: 9.776 mcs
Message throughput: 102287 msg/s
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

Total time: 10.019 s
Total data: 66.374 MiB
Total messages: 2174676
Data throughput: 6.638 MiB/s
Message latency: 4.607 mcs
Message throughput: 217051 msg/s
```

* [TcpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastServer/Program.cs)
* [TcpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 1111
Working clients: 100
Message size: 32

Errors: 0

Total time: 10.031 s
Total data: 127.428 MiB
Total messages: 4175253
Data throughput: 12.718 MiB/s
Message latency: 2.402 mcs
Message throughput: 416205 msg/s
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

Total time: 10.091 s
Total data: 46.905 MiB
Total messages: 1536317
Data throughput: 4.661 MiB/s
Message latency: 6.568 mcs
Message throughput: 152236 msg/s
```

* [SslMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastServer/Program.cs)
* [SslMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 100
Message size: 32

Errors: 0

Total time: 10.278 s
Total data: 66.540 MiB
Total messages: 2179997
Data throughput: 6.483 MiB/s
Message latency: 4.715 mcs
Message throughput: 212083 msg/s
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

Total time: 10.020 s
Total data: 15.961 MiB
Total messages: 522293
Data throughput: 1.604 MiB/s
Message latency: 19.185 mcs
Message throughput: 52123 msg/s
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

Total time: 10.029 s
Total data: 55.614 MiB
Total messages: 1821897
Data throughput: 5.556 MiB/s
Message latency: 5.504 mcs
Message throughput: 181656 msg/s
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

Total time: 10.002 s
Total data: 46.403 MiB
Total messages: 458939
Data throughput: 4.653 MiB/s
Message latency: 21.794 mcs
Message throughput: 45883 msg/s
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

Total time: 10.015 s
Total data: 299.400 MiB
Total messages: 2961649
Data throughput: 29.915 MiB/s
Message latency: 3.381 mcs
Message throughput: 295717 msg/s
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

Total time: 10.003 s
Total data: 22.625 MiB
Total messages: 223672
Data throughput: 2.266 MiB/s
Message latency: 44.724 mcs
Message throughput: 22359 msg/s
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

Total time: 10.162 s
Total data: 92.492 MiB
Total messages: 914845
Data throughput: 9.103 MiB/s
Message latency: 11.107 mcs
Message throughput: 90025 msg/s
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
