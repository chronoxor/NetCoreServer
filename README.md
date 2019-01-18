# NetCoreServer

[![Windows build status](https://img.shields.io/appveyor/ci/chronoxor/NetCoreServer/master.svg?label=Windows)](https://ci.appveyor.com/project/chronoxor/NetCoreServer)

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
    * [Example: Asio timer](#example-asio-timer)
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
* [Asynchronous communication](https://think-async.com)
* Supported CPU scalability designs: IO service per thread, thread pool
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

## Example: Asio timer
Here comes the example of Asio timer. It can be used to wait for some action
in future with providing absolute time or relative time span. Asio timer can
be used in synchronous or asynchronous modes.
```c#
using System;
using System.Threading;
using NetCoreServer;

namespace AsioTimer
{
    class AsioTimer : NetCoreServer.Timer
    {
        public AsioTimer(Service service) : base(service) {}

        protected override void OnTimer(bool canceled)
        {
            Console.WriteLine("Asio timer " + (canceled ? "canceled" : "expired"));
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Asio timer caught an error with code {error} and category '{category}': {message}");
        }
    }

    class Program
    {
        static void Main()
        {
            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create a new Asio timer
            var timer = new AsioTimer(service);

            // Setup and synchronously wait for the timer
            timer.Setup(DateTime.UtcNow.AddSeconds(1));
            timer.WaitSync();

            // Setup and asynchronously wait for the timer
            timer.Setup(TimeSpan.FromSeconds(1));
            timer.WaitAsync();

            // Wait for a while...
            Thread.Sleep(2000);

            // Setup and asynchronously wait for the timer
            timer.Setup(TimeSpan.FromSeconds(1));
            timer.WaitAsync();

            // Wait for a while...
            Thread.Sleep(500);

            // Cancel the timer
            timer.Cancel();

            // Wait for a while...
            Thread.Sleep(500);

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
            Console.WriteLine("Done!");
        }
    }
}
```

Output of the above example is the following:
```
Service starting...Done!
Asio timer expired
Asio timer canceled
Service stopping...Done!
```

## Example: TCP chat server
Here comes the example of the TCP chat server. It handles multiple TCP client
sessions and multicast received message from any session to all ones. Also it
is possible to send admin message directly from the server.

```c#
using System;
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

        protected override void OnReceived(byte[] buffer)
        {
            string message = Encoding.UTF8.GetString(buffer);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Chat TCP session caught an error with code {error} and category '{category}': {message}");
        }
    }

    class ChatServer : TcpServer
    {
        public ChatServer(Service service, InternetProtocol protocol, int port) : base(service, protocol, port) {}

        protected override TcpSession CreateSession()
        {
            return new ChatSession(this);
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Chat TCP server caught an error with code {error} and category '{category}': {message}");
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

            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create a new TCP chat server
            var server = new ChatServer(service, InternetProtocol.IPv4, port);

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (line == String.Empty)
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

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
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
using System.Text;
using System.Threading;
using NetCoreServer;

namespace TcpChatClient
{
    class ChatClient : TcpClient
    {
        public ChatClient(Service service, string address, int port) : base(service, address, port) {}

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

        protected override void OnReceived(byte[] buffer)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer));
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Chat TCP client caught an error with code {error} and category '{category}': {message}");
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

            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create a new TCP chat client
            var client = new ChatClient(service, address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.Connect();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (line == String.Empty)
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

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
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
using System.Text;
using NetCoreServer;

namespace SslChatServer
{
    class ChatSession : SslSession
    {
        public ChatSession(SslServer server) : base(server) { }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat SSL session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from SSL chat! Please send a message or '!' to disconnect the client!";
            Send(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat SSL session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer)
        {
            string message = Encoding.UTF8.GetString(buffer);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Chat SSL session caught an error with code {error} and category '{category}': {message}");
        }
    }

    class ChatServer : SslServer
    {
        public ChatServer(Service service, SslContext context, InternetProtocol protocol, int port) : base(service, context, protocol, port) {}

        protected override SslSession CreateSession()
        {
            return new ChatSession(this);
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Chat SSL server caught an error with code {error} and category '{category}': {message}");
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

            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create and prepare a new SSL server context
            var context = new SslContext(SslMethod.TLSV12);
            context.SetPassword("qwerty");
            context.UseCertificateChainFile("../../../../Tools/certificates/server.pem");
            context.UsePrivateKeyFile("../../../../Tools/certificates/server.pem", SslFileFormat.PEM);
            context.UseTmpDHFile("../../../../Tools/certificates/dh4096.pem");

            // Create a new SSL chat server
            var server = new ChatServer(service, context, InternetProtocol.IPv4, port);

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (line == String.Empty)
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

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
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
using System.Text;
using System.Threading;
using NetCoreServer;

namespace SslChatClient
{
    class ChatClient : SslClient
    {
        public ChatClient(Service service, SslContext context, string address, int port) : base(service, context, address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            Disconnect();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat SSL client connected a new session with Id {Id}");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat SSL client disconnected a session with Id {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                Connect();
        }

        protected override void OnReceived(byte[] buffer)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer));
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Chat SSL client caught an error with code {error} and category '{category}': {message}");
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

            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create and prepare a new SSL client context
            var context = new SslContext(SslMethod.TLSV12);
            context.SetVerifyMode(SslVerifyMode.VerifyPeer);
            context.LoadVerifyFile("../../../../Tools/certificates/ca.pem");

            // Create a new SSL chat client
            var client = new ChatClient(service, context, address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.Connect();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (line == String.Empty)
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

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
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
using System.Text;
using NetCoreServer;

namespace UdpEchoServer
{
    class EchoServer : UdpServer
    {
        public EchoServer(Service service, InternetProtocol protocol, int port) : base(service, protocol, port) {}

        protected override void OnReceived(UdpEndpoint endpoint, byte[] buffer)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer));

            // Echo the message back to the sender
            Send(endpoint, buffer);
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Echo UDP server caught an error with code {error} and category '{category}': {message}");
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

            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create a new UDP echo server
            var server = new EchoServer(service, InternetProtocol.IPv4, port);

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (line == String.Empty)
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

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
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
using System.Text;
using System.Threading;
using NetCoreServer;

namespace UdpEchoClient
{
    class EchoClient : UdpClient
    {
        public EchoClient(Service service, string address, int port) : base(service, address, port) {}

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

        protected override void OnReceived(UdpEndpoint endpoint, byte[] buffer)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer));
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Echo UDP client caught an error with code {error} and category '{category}': {message}");
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

            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create a new TCP chat client
            var client = new EchoClient(service, address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.Connect();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (line == String.Empty)
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

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
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
using System.Text;
using NetCoreServer;

namespace UdpMulticastServer
{
    class MulticastServer : UdpServer
    {
        public MulticastServer(Service service, InternetProtocol protocol, int port) : base(service, protocol, port) {}

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Multicast UDP server caught an error with code {error} and category '{category}': {message}");
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

            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create a new UDP multicast server
            var server = new MulticastServer(service, InternetProtocol.IPv4, 0);

            // Start the multicast server
            Console.Write("Server starting...");
            server.Start(multicastAddress, multicastPort);
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (line == String.Empty)
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

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
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
using System.Text;
using System.Threading;
using NetCoreServer;

namespace UdpMulticastClient
{
    class MulticastClient : UdpClient
    {
        public string Multicast;

        public MulticastClient(Service service, string address, int port) : base(service, address, port) {}

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

        protected override void OnReceived(UdpEndpoint endpoint, byte[] buffer)
        {
            Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer));
        }

        protected override void OnError(int error, string category, string message)
        {
            Console.WriteLine($"Multicast UDP client caught an error with code {error} and category '{category}': {message}");
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

            // Create a new service
            var service = new Service();

            // Start the service
            Console.Write("Service starting...");
            service.Start();
            Console.WriteLine("Done!");

            // Create a new TCP chat client
            var client = new MulticastClient(service, listenAddress, multicastPort);
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
                if (line == String.Empty)
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

            // Stop the service
            Console.Write("Service stopping...");
            service.Stop();
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
* [TcpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoClient/Program.cs) -c 1 -m 1000000 -t 1

```
Server address: 127.0.0.1
Server port: 1111
Working threads: 1
Working clients: 1
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 3.893 s
Total data: 30.530 MiB
Total messages: 1000000
Data throughput: 7.858 MiB/s
Message latency: 3.893 mcs
Message throughput: 256842 msg/s
```

* [TcpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoServer/Program.cs)
* [TcpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpEchoClient/Program.cs) -c 100 -m 1000000 -t 4

```
Server address: 127.0.0.1
Server port: 1111
Working threads: 4
Working clients: 100
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 1.557 s
Total data: 30.518 MiB
Total messages: 999632
Data throughput: 19.597 MiB/s
Message latency: 1.558 mcs
Message throughput: 641705 msg/s
```

### SSL echo server

* [SslEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoServer/Program.cs)
* [SslEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoClient/Program.cs) -c 1 -m 1000000 -t 1

```
Server address: 127.0.0.1
Server port: 2222
Working threads: 1
Working clients: 1
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 7.201 s
Total data: 30.530 MiB
Total messages: 1000000
Data throughput: 4.243 MiB/s
Message latency: 7.201 mcs
Message throughput: 138858 msg/s
```

* [SslEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoServer/Program.cs)
* [SslEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslEchoClient/Program.cs) -c 100 -m 1000000 -t 4

```
Server address: 127.0.0.1
Server port: 2222
Working threads: 4
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
* [UdpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoClient/Program.cs) -c 1 -m 1000000 -t 1

```
Server address: 127.0.0.1
Server port: 3333
Working threads: 1
Working clients: 1
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 22.071 s
Total data: 30.530 MiB
Total messages: 1000000
Data throughput: 1.391 MiB/s
Message latency: 22.071 mcs
Message throughput: 45306 msg/s
```

* [UdpEchoServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoServer/Program.cs)
* [UdpEchoClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpEchoClient/Program.cs) -c 100 -m 1000000 -t 4

```
Server address: 127.0.0.1
Server port: 3333
Working threads: 4
Working clients: 100
Messages to send: 1000000
Message size: 32

Errors: 0

Round-trip time: 6.086 s
Total data: 30.512 MiB
Total messages: 999471
Data throughput: 5.011 MiB/s
Message latency: 6.090 mcs
Message throughput: 164201 msg/s
```

## Benchmark: Multicast

![Multicast](https://github.com/chronoxor/NetCoreServer/raw/master/images/multicast.png)

In this scenario server multicasts messages to all connected clients.
The benchmark counts total messages received by all clients for all
the working time and measures messages & data throughput, count
of errors.

### TCP multicast server

* [TcpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastServer/Program.cs)
* [TcpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastClient/Program.cs) -c 1 -t 1

```
Server address: 127.0.0.1
Server port: 1111
Working threads: 1
Working clients: 1
Message size: 32

Errors: 0

Multicast time: 10.002 s
Total data: 970.554 MiB
Total messages: 31802690
Data throughput: 97.027 MiB/s
Message latency: 314 ns
Message throughput: 3179388 msg/s
```

* [TcpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastServer/Program.cs)
* [TcpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/TcpMulticastClient/Program.cs) -c 100 -t 4

```
Server address: 127.0.0.1
Server port: 1111
Working threads: 4
Working clients: 100
Message size: 32

Errors: 0

Multicast time: 10.007 s
Total data: 3.442 GiB
Total messages: 115147638
Data throughput: 351.135 MiB/s
Message latency: 86 ns
Message throughput: 11505891 msg/s
```

### SSL multicast server

* [SslMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastServer/Program.cs)
* [SslMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastClient/Program.cs) -c 1 -t 1

```
Server address: 127.0.0.1
Server port: 2222
Working threads: 1
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
* [SslMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/SslMulticastClient/Program.cs) -c 100 -t 4

```
Server address: 127.0.0.1
Server port: 2222
Working threads: 4
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
* [UdpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastClient/Program.cs) -c 1 -t 1

```
Server address: 239.255.0.1
Server port: 3333
Working threads: 1
Working clients: 1
Message size: 32

Errors: 0

Multicast time: 10.004 s
Total data: 19.644 MiB
Total messages: 643205
Data throughput: 1.985 MiB/s
Message latency: 15.553 mcs
Message throughput: 64292 msg/s
```

* [UdpMulticastServer](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastServer/Program.cs)
* [UdpMulticastClient](https://github.com/chronoxor/NetCoreServer/blob/master/performance/UdpMulticastClient/Program.cs) -c 100 -t 4

```
Server address: 239.255.0.1
Server port: 3333
Working threads: 4
Working clients: 100
Message size: 32

Errors: 0

Multicast time: 10.007 s
Total data: 78.498 MiB
Total messages: 2571847
Data throughput: 7.862 MiB/s
Message latency: 3.891 mcs
Message throughput: 256982 msg/s
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
