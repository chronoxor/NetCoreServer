using System;
using System.Net.Sockets;
using System.Threading;
using TcpClient = NetCoreServer.TcpClient;

using com.chronoxor.simple;
using com.chronoxor.simple.FBE;

namespace ProtoClient
{
    public class TcpProtoClient : TcpClient
    {
        public TcpProtoClient(string address, int port) : base(address, port) {}

        public bool ConnectAndStart()
        {
            Console.WriteLine($"TCP protocol client starting a new session with Id '{Id}'...");

            StartReconnectTimer();
            return ConnectAsync();
        }

        public bool DisconnectAndStop()
        {
            Console.WriteLine($"TCP protocol client stopping the session with Id '{Id}'...");

            StopReconnectTimer();
            DisconnectAsync();
            return true;
        }

        public override bool Reconnect()
        {
            return ReconnectAsync();
        }

        private Timer _reconnectTimer;

        public void StartReconnectTimer()
        {
            // Start the reconnect timer
            _reconnectTimer = new Timer(state =>
            {
                Console.WriteLine($"TCP reconnect timer connecting the client session with Id '{Id}'...");
                ConnectAsync();
            }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void StopReconnectTimer()
        {
            // Stop the reconnect timer
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }

        public delegate void ConnectedHandler();
        public event ConnectedHandler Connected = () => {};

        protected override void OnConnected()
        {
            Console.WriteLine($"TCP protocol client connected a new session with Id '{Id}' to remote address '{Address}' and port {Port}");

            Connected?.Invoke();
        }

        public delegate void DisconnectedHandler();
        public event DisconnectedHandler Disconnected = () => {};

        protected override void OnDisconnected()
        {
            Console.WriteLine($"TCP protocol client disconnected the session with Id '{Id}'");

            // Setup and asynchronously wait for the reconnect timer
            _reconnectTimer?.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);

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
            Console.WriteLine($"TCP protocol client caught a socket error: {error}");
        }

        #region IDisposable implementation

        // Disposed flag.
        private bool _disposed;

        protected override void Dispose(bool disposingManagedResources)
        {
            if (!_disposed)
            {
                if (disposingManagedResources)
                {
                    // Dispose managed resources here...
                    StopReconnectTimer();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                _disposed = true;
            }

            // Call Dispose in the base class.
            base.Dispose(disposingManagedResources);
        }

        // The derived class does not have a Finalize method
        // or a Dispose method without parameters because it inherits
        // them from the base class.

        #endregion
    }

    public class SimpleProtoClient : Client, ISenderListener, IReceiverListener, IDisposable
    {
        private readonly TcpProtoClient _tcpProtoClient;

        public Guid Id => _tcpProtoClient.Id;
        public bool IsConnected => _tcpProtoClient.IsConnected;

        public SimpleProtoClient(string address, int port)
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

        public bool ConnectAndStart() { return _tcpProtoClient.ConnectAndStart(); }
        public bool DisconnectAndStop() { return _tcpProtoClient.DisconnectAndStop(); }
        public bool Reconnect() { return _tcpProtoClient.Reconnect(); }

        private bool _watchdog;
        private Thread _watchdogThread;

        public bool StartWatchdog()
        {
            if (_watchdog)
                return false;

            Console.WriteLine("Watchdog thread starting...");

            // Start the watchdog thread
            _watchdog = true;
            _watchdogThread = new Thread(WatchdogThread);

            Console.WriteLine("Watchdog thread started!");

            return true;
        }

        public bool StopWatchdog()
        {
            if (!_watchdog)
                return false;

            Console.WriteLine("Watchdog thread stopping...");

            // Stop the watchdog thread
            _watchdog = false;
            _watchdogThread.Join();

            Console.WriteLine("Watchdog thread stopped!");

            return true;
        }

        public static void WatchdogThread(object obj)
        {
            var instance = obj as SimpleProtoClient;
            if (instance == null)
                return;

            try
            {
                // Watchdog loop...
                while (instance._watchdog)
                {
                    var utc = DateTime.UtcNow;

                    // Watchdog the client
                    instance.Watchdog(utc);

                    // Sleep for a while...
                    Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Config client watchdog thread terminated: {e}");
            }
        }

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

        private void HandleDisconnectRequest(DisconnectRequest request) { Console.WriteLine($"Received: {request}"); _tcpProtoClient.DisconnectAsync(); }
        private void HandleSimpleResponse(SimpleResponse response) { Console.WriteLine($"Received: {response}"); }
        private void HandleSimpleReject(SimpleReject reject) { Console.WriteLine($"Received: {reject}"); }
        private void HandleSimpleNotify(SimpleNotify notify) { Console.WriteLine($"Received: {notify}"); }

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
        static void Main(string[] args)
        {
            // Simple protocol server address
            string address = "127.0.0.1";
            if (args.Length > 0)
                address = args[0];

            // Simple protocol server port
            int port = 4444;
            if (args.Length > 1)
                port = int.Parse(args[1]);

            Console.WriteLine($"Simple protocol server address: {address}");
            Console.WriteLine($"Simple protocol server port: {port}");

            Console.WriteLine();

            // Create a new simple protocol chat client
            var client = new SimpleProtoClient(address, port);

            // Connect the client
            Console.Write("Client connecting...");
            client.ConnectAndStart();
            Console.WriteLine("Done!");

            client.StartWatchdog();

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
                    client.Reconnect();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Send request to the simple protocol server
                SimpleRequest request = SimpleRequest.Default;
                request.Message = line;
                var response = client.Request(request).Result;

                // Show string hash calculation result
                Console.WriteLine($"Hash of '{line}' = 0x{response.Hash:X8}");
            }

            client.StopWatchdog();

            // Disconnect the client
            Console.Write("Client disconnecting...");
            client.DisconnectAndStop();
            Console.WriteLine("Done!");
        }
    }
}
