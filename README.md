# NetCoreServer

[![Windows build status](https://img.shields.io/appveyor/ci/chronoxor/NetCoreServer/master.svg?label=Windows)](https://ci.appveyor.com/project/chronoxor/NetCoreServer)
[![NuGet](https://img.shields.io/nuget/v/NetCoreServer.svg)](https://www.nuget.org/packages/NetCoreServer/)

Ultra fast and low latency asynchronous socket server & client C# .NET Core
library with support TCP, SSL, UDP protocols and [10K connections problem](https://en.wikipedia.org/wiki/C10k_problem)
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
  * [Performance](#performance)
    * [Benchmark: Round-Trip](#benchmark-round-trip)
      * [TCP echo server](#tcp-echo-server)
      * [SSL echo server](#ssl-echo-server)
      * [UDP echo server](#udp-echo-server)
    * [Benchmark: Multicast](#benchmark-multicast)
      * [TCP multicast server](#tcp-multicast-server)
      * [SSL multicast server](#ssl-multicast-server)
      * [UDP multicast server](#udp-multicast-server)
  * [OpenSSL certificates](#openssl-certificates)
    * [Certificate Authority](#certificate-authority)
    * [SSL Server certificate](#ssl-server-certificate)
    * [SSL Client certificate](#ssl-client-certificate)
    * [Diffie-Hellman key exchange](#diffie-hellman-key-exchange)

# Features
* Asynchronous communication
* Supported transport protocols: [TCP](#example-tcp-chat-server), [SSL](#example-ssl-chat-server),
  [UDP](#example-udp-echo-server), [UDP multicast](#example-udp-multicast-server)

# Requirements
* Windows 10
* [7-Zip](https://www.7-zip.org)
* [cmake](https://www.cmake.org)
* [git](https://git-scm.com)
* [Visual Studio](https://www.visualstudio.com)

# How to build?

### Setup repository
```shell
git clone https://github.com/chronoxor/NetCoreServer.git
cd NetCoreServer
```

### Windows (Visual Studio)
Open and build NetCoreServer.sln or run the build script:
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
        public ChatSession(TcpServer server) : base(server) { }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";
            Send(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, (int)size);
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

        protected override TcpSession CreateSession()
        {
            return new ChatSession(this);
        }

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
                if (line == string.Empty)
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
            Disconnect();
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
                Connect();
        }

        protected override void OnReceived(byte[] buffer, long size)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, (int)size));
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

            // Create a new TCP chat client
            var client = new ChatClient(address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.Connect();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (line == string.Empty)
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

## Example: SSL chat server
Here comes the example of the SSL chat server. It handles multiple SSL client
sessions and multicast received message from any session to all ones. Also it
is possible to send admin message directly from the server.

This example is very similar to the TCP one except the code that prepares SSL
context and handshake handler.

```c#

```

## Example: SSL chat client
Here comes the example of the SSL chat client. It connects to the SSL chat
server and allows to send message to it and receive new messages.

This example is very similar to the TCP one except the code that prepares SSL
context and handshake handler.

```c#

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
            Receive();
        }

        protected override void OnReceived(IPEndPoint endpoint, byte[] buffer, long size)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer, 0, (int)size));

            // Echo the message back to the sender
            SendAsync(endpoint, buffer, 0, size);
        }

        protected override void OnSent(IPEndPoint endpoint, long sent)
        {
            // Continue receive datagrams
            Receive();
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
                if (line == string.Empty)
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

namespace UdpEchoClient
{
    class EchoClient : NetCoreServer.UdpClient
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
            Receive();
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

        protected override void OnReceived(IPEndPoint endpoint, byte[] buffer, long size)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer, 0, (int)size));

            // Continue receive datagrams
            Receive();
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
                if (line == string.Empty)
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
                client.SendSync(line);
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
                if (line == string.Empty)
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
                server.MulticastSync(line);
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

namespace UdpMulticastClient
{
    class MulticastClient : NetCoreServer.UdpClient
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
            Receive();
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

        protected override void OnReceived(IPEndPoint endpoint, byte[] buffer, long size)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer, 0, (int)size));

            // Continue receive datagrams
            Receive();
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
                if (line == string.Empty)
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
* [TcpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoClient/Program.cs) -c 1 -m 1000000

```
Server address: 127.0.0.1
Server port: 1111
Working clients: 1
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 3.285 s
Total data: 30.530 MiB
Total messages: 1000000
Data throughput: 9.296 MiB/s
Message latency: 3.285 mcs
Message throughput: 304392 msg/s
```

* [TcpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoServer/Program.cs)
* [TcpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoClient/Program.cs) -c 100 -m 1000000

```
Server address: 127.0.0.1
Server port: 1111
Working clients: 100
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 1.307 s
Total data: 30.483 MiB
Total messages: 998524
Data throughput: 23.308 MiB/s
Message latency: 1.309 mcs
Message throughput: 763537 msg/s
```

### SSL echo server

* [SslEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoServer/Program.cs)
* [SslEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoClient/Program.cs) -c 1 -m 1000000

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 1
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 1.276 s
Total data: 30.520 MiB
Total messages: 999703
Data throughput: 23.920 MiB/s
Message latency: 1.276 mcs
Message throughput: 783110 msg/s
```

* [SslEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoServer/Program.cs)
* [SslEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoClient/Program.cs) -c 100 -m 1000000

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 100
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 4.365 s
Total data: 30.328 MiB
Total messages: 993547
Data throughput: 6.968 MiB/s
Message latency: 4.393 mcs
Message throughput: 227600 msg/s
```

### UDP echo server

* [UdpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoServer/Program.cs)
* [UdpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoClient/Program.cs) -c 1 -m 1000000

```
Server address: 127.0.0.1
Server port: 3333
Working clients: 1
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 26.167 s
Total data: 30.530 MiB
Total messages: 1000000
Data throughput: 1.170 MiB/s
Message latency: 26.167 mcs
Message throughput: 38214 msg/s
```

* [UdpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoServer/Program.cs)
* [UdpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoClient/Program.cs) -c 100 -m 1000000

```
Server address: 127.0.0.1
Server port: 3333
Working clients: 100
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 7.834 s
Total data: 30.530 MiB
Total messages: 1000000
Data throughput: 3.916 MiB/s
Message latency: 7.834 mcs
Message throughput: 127642 msg/s
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

Errors: 0

Multicast time: 10.019 s
Total data: 34.898 MiB
Total messages: 1142855
Data throughput: 3.492 MiB/s
Message latency: 8.767 mcs
Message throughput: 114059 msg/s
```

* [TcpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastServer/Program.cs)
* [TcpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 1111
Working clients: 100
Message size: 32

Errors: 0

Multicast time: 10.040 s
Total data: 300.953 MiB
Total messages: 9860913
Data throughput: 29.995 MiB/s
Message latency: 1.018 mcs
Message throughput: 982115 msg/s
```

### SSL multicast server

* [SslMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastServer/Program.cs)
* [SslMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastClient/Program.cs) -c 1

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 1
Message size: 32

Errors: 0

Multicast time: 10.011 s
Total data: 458.031 MiB
Total messages: 15008762
Data throughput: 45.769 MiB/s
Message latency: 667 ns
Message throughput: 1499196 msg/s
```

* [SslMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastServer/Program.cs)
* [SslMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastClient/Program.cs) -c 100

```
Server address: 127.0.0.1
Server port: 2222
Working clients: 100
Message size: 32

Errors: 0

Multicast time: 10.350 s
Total data: 3.082 GiB
Total messages: 103351505
Data throughput: 304.726 MiB/s
Message latency: 100 ns
Message throughput: 9984724 msg/s
```

### UDP multicast server

* [UdpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastServer/Program.cs)
* [UdpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastClient/Program.cs) -c 1

```
Server address: 239.255.0.1
Server port: 3333
Working clients: 1
Message size: 32

Errors: 0

Multicast time: 10.018 s
Total data: 20.213 MiB
Total messages: 662188
Data throughput: 2.017 MiB/s
Message latency: 15.130 mcs
Message throughput: 66093 msg/s
```

* [UdpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastServer/Program.cs)
* [UdpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastClient/Program.cs) -c 100

```
Server address: 239.255.0.1
Server port: 3333
Working clients: 100
Message size: 32

Errors: 0

Multicast time: 10.036 s
Total data: 35.923 MiB
Total messages: 1176426
Data throughput: 3.591 MiB/s
Message latency: 8.531 mcs
Message throughput: 117216 msg/s
```

# OpenSSL certificates
In order to create OpenSSL based server and client you should prepare a set of
SSL certificates. Here comes several steps to get a self-signed set of SSL
certificates for testing purposes:

## Certificate Authority

* Create CA private key
```shell
openssl genrsa -des3 -passout pass:qwerty -out ca-secret.key 4096
```

* Remove passphrase
```shell
openssl rsa -passin pass:qwerty -in ca-secret.key -out ca.key
```

* Create CA self-signed certificate
```shell
openssl req -new -x509 -days 3650 -subj '/C=BY/ST=Belarus/L=Minsk/O=Example root CA/OU=Example CA unit/CN=example.com' -key ca.key -out ca.crt -config openssl.cfg
```

* Convert CA self-signed certificate to PKCS
```shell
openssl pkcs12 -clcerts -export -passout pass:qwerty -in ca.crt -inkey ca.key -out ca.p12
```

* Convert CA self-signed certificate to PEM
```shell
openssl pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in ca.p12 -out ca.pem
```

## SSL Server certificate

* Create private key for the server
```shell
openssl genrsa -des3 -passout pass:qwerty -out server-secret.key 4096
```

* Remove passphrase
```shell
openssl rsa -passin pass:qwerty -in server-secret.key -out server.key
```

* Create CSR for the server
```shell
openssl req -new -subj '/C=BY/ST=Belarus/L=Minsk/O=Example server/OU=Example server unit/CN=server.example.com' -key server.key -out server.csr -config openssl.cfg
```

* Create certificate for the server
```shell
openssl x509 -req -days 3650 -in server.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out server.crt
```

* Convert the server certificate to PKCS
```shell
openssl pkcs12 -clcerts -export -passout pass:qwerty -in server.crt -inkey server.key -out server.p12
```

* Convert the server certificate to PEM
```shell
openssl pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in server.p12 -out server.pem
```

## SSL Client certificate

* Create private key for the client
```shell
openssl genrsa -des3 -passout pass:qwerty -out client-secret.key 4096
```

* Remove passphrase
```shell
openssl rsa -passin pass:qwerty -in client-secret.key -out client.key
```

* Create CSR for the client
```shell
openssl req -new -subj '/C=BY/ST=Belarus/L=Minsk/O=Example client/OU=Example client unit/CN=client.example.com' -key client.key -out client.csr -config openssl.cfg
```

* Create the client certificate
```shell
openssl x509 -req -days 3650 -in client.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out client.crt
```

* Convert the client certificate to PKCS
```shell
openssl pkcs12 -clcerts -export -passout pass:qwerty -in client.crt -inkey client.key -out client.p12
```

* Convert the client certificate to PEM
```shell
openssl pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in client.p12 -out client.pem
```

## Diffie-Hellman key exchange

* Create DH parameters
```shell
openssl dhparam -out dh4096.pem 4096
```
