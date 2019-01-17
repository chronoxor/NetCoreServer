using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetCoreServer
{
    /// <summary>
    /// TCP server is used to connect, disconnect and manage TCP sessions
    /// </summary>
    /// <remarks>Thread-safe</remarks>
    public class TcpServer
    {
        /// <summary>
        /// Initialize TCP server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public TcpServer(IPAddress address, int port) : this(new IPEndPoint(address, port)) {}
        /// <summary>
        /// Initialize TCP server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public TcpServer(string address, int port) : this(new IPEndPoint(IPAddress.Parse(address), port)) {}
        /// <summary>
        /// Initialize TCP server with a given IP endpoint
        /// </summary>
        /// <param name="endpoint">IP endpoint</param>
        public TcpServer(IPEndPoint endpoint) { Endpoint = endpoint; }

        /// <summary>
        /// IP endpoint
        /// </summary>
        public IPEndPoint Endpoint { get; private set; }

        /// <summary>
        /// Number of sessions connected to the server
        /// </summary>
        public long ConnectedSessions { get; internal set; }
        /// <summary>
        /// Number of bytes pending sent by the server
        /// </summary>
        public long BytesPending { get; internal set; }
        /// <summary>
        /// Number of bytes sent by the server
        /// </summary>
        public long BytesSent { get; internal set; }
        /// <summary>
        /// Number of bytes received by the server
        /// </summary>
        public long BytesReceived { get; internal set; }

        /// <summary>
        /// Option: keep alive
        /// </summary>
        /// <remarks>
        /// This option will setup SO_KEEPALIVE if the OS support this feature
        /// </remarks>
        public bool OptionKeepAlive { get; set; }
        /// <summary>
        /// Option: no delay
        /// </summary>
        /// <remarks>
        /// This option will enable/disable Nagle's algorithm for TCP protocol
        /// </remarks>
        public bool OptionNoDelay { get; set; }
        /// <summary>
        /// Option: reuse address
        /// </summary>
        /// <remarks>
        /// This option will enable/disable SO_REUSEADDR if the OS support this feature
        /// </remarks>
        public bool OptionReuseAddress { get; set; }
        /// <summary>
        /// Option: reuse port
        /// </summary>
        /// <remarks>
        /// This option will enable/disable SO_REUSEPORT if the OS support this feature
        /// </remarks>
        public bool OptionReusePort { get; set; }

        #region Start/Stop server

        // Server acceptor
        private int _acceptorBacklog = 1024;
        private Socket _acceptorSocket;
        private SocketAsyncEventArgs _acceptorEventArg;

        /// <summary>
        /// Is the server started?
        /// </summary>
        public bool IsStarted { get; private set; }
        /// <summary>
        /// Is the server accepting new clients?
        /// </summary>
        public bool IsAccepting { get; private set; }

        /// <summary>
        /// Start the server
        /// </summary>
        /// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
        public bool Start()
        {
            Debug.Assert(!IsStarted, "TCP server is already started!");
            if (IsStarted)
                return false;

            // Create a new acceptor socket
            _acceptorSocket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            // Bind the acceptor socket to the IP endpoint
            _acceptorSocket.Bind(Endpoint);
            // Start listen to the acceptor socket with the given accepting backlog size
            _acceptorSocket.Listen(_acceptorBacklog);

            // Reset statistic
            BytesPending = 0;
            BytesSent = 0;
            BytesReceived = 0;

            // Update the started flag
            IsStarted = true;

            // Call the server started handler
            OnStarted();

            // Perform the first server accept
            IsAccepting = true;
            _acceptorEventArg = new SocketAsyncEventArgs();
            _acceptorEventArg.Completed += AcceptorEventArg_Completed;
            StartAccept(_acceptorEventArg);

            return true;
        }

        /// <summary>
        /// Stop the server
        /// </summary>
        /// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
        public bool Stop()
        {
            Debug.Assert(IsStarted, "TCP server is not started!");
            if (!IsStarted)
                return false;

            // Stop accepting new clients
            IsAccepting = false;

            // Close the acceptor socket
            _acceptorSocket.Close();

            // Disconnect all sessions
            DisconnectAll();

            // Update the started flag
            IsStarted = false;

            // Clear multicast buffer
            ClearBuffers();

            // Call the server stopped handler
            OnStopped();

            return true;
        }

        /// <summary>
        /// Restart the server
        /// </summary>
        /// <returns>'true' if the server was successfully restarted, 'false' if the server failed to restart</returns>
        public bool Restart()
        {
            if (!Stop())
                return false;

            while (IsStarted)
                Thread.Yield();

            return Start();
        }

        #endregion

        #region Accepting clients

        /// <summary>
        /// Start accept a new client connection
        /// </summary>
        private void StartAccept(SocketAsyncEventArgs e)
        {
            // Socket must be cleared since the context object is being reused
            e.AcceptSocket = null;

            if (!_acceptorSocket.AcceptAsync(e))
                ProcessAccept(e);
        }

        /// <summary>
        /// Process accepted client connection
        /// </summary>
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                // Create a new session to register
                var session = CreateSession();

                // Register the session
                RegisterSession(session);

                // Connect new session
                session.Connect(e.AcceptSocket);
            }
            else
                OnError(e.SocketError);

            // Accept the next client connection
            if (IsAccepting)
                StartAccept(e);
        }

        /// <summary>
        /// This method is the callback method associated with Socket.AcceptAsync()
        /// operations and is invoked when an accept operation is complete
        /// </summary>
        private void AcceptorEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        #endregion

        #region Session factory

        /// <summary>
        /// Create TCP session factory method
        /// </summary>
        /// <returns>TCP session</returns>
        protected virtual TcpSession CreateSession() { return new TcpSession(this); }

        #endregion

        #region Session management

        // Server sessions
        private readonly object _sessionsLock = new object();
        private readonly Dictionary<Guid, TcpSession> _sessions = new Dictionary<Guid, TcpSession>();

        /// <summary>
        /// Disconnect all connected sessions
        /// </summary>
        /// <returns>'true' if all sessions were successfully disconnected, 'false' if the server is not started</returns>
        public bool DisconnectAll()
        {
            if (!IsStarted)
                return false;

            TcpSession[] disconnect;

            lock (_sessionsLock)
            {
                // Copy sessions to disconnect
                disconnect = _sessions.Values.ToArray();
            }

            // Disconnect all sessions
            foreach (var session in disconnect)
                session.Disconnect();

            return true;
        }

        /// <summary>
        /// Find a session with a given Id
        /// </summary>
        /// <param name="id">Session Id</param>
        /// <returns>Session with a given Id or null if the session it not connected</returns>
        public TcpSession FindSession(Guid id)
        {
            lock (_sessionsLock)
            {
                // Try to find the required session
                return _sessions.TryGetValue(id, out TcpSession result) ? result : null;
            }
        }

        /// <summary>
        /// Register a new session
        /// </summary>
        /// <param name="session">Session to register</param>
        internal void RegisterSession(TcpSession session)
        {
            lock (_sessionsLock)
            {
                // Register a new session
                _sessions[session.Id] = session;
            }
        }

        /// <summary>
        /// Unregister session by Id
        /// </summary>
        /// <param name="id">Session Id</param>
        internal void UnregisterSession(Guid id)
        {
            lock (_sessionsLock)
            {
                // Unregister session by Id
                _sessions.Remove(id);
            }
        }

        #endregion

        #region Multicasting

        // Multicast buffer
        private readonly object _multicastLock = new object();
        private readonly List<byte> _multicastBuffer = new List<byte>();

        /// <summary>
        /// Multicast data to all connected sessions
        /// </summary>
        /// <param name="buffer">Buffer to multicast</param>
        /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
        public bool Multicast(byte[] buffer) { return Multicast(buffer, 0, buffer.Length); }

        /// <summary>
        /// Multicast data to all connected clients
        /// </summary>
        /// <param name="buffer">Buffer to multicast</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
        public bool Multicast(byte[] buffer, long offset, long size)
        {
            return true;
        }

        /// <summary>
        /// Multicast text to all connected clients
        /// </summary>
        /// <param name="text">Text string to multicast</param>
        /// <returns>'true' if the text was successfully multicasted, 'false' if the text was not multicasted</returns>
        public bool Multicast(string text)
        {
            return true;
        }

        /// <summary>
        /// Clear multicast buffer
        /// </summary>
        private void ClearBuffers()
        {
            lock (_multicastLock)
            {
                _multicastBuffer.Clear();

                // Update statistic
                BytesPending = 0;
            }
        }

        #endregion

        #region Server handlers

        /// <summary>
        /// Handle server started notification
        /// </summary>
        protected virtual void OnStarted() {}
        /// <summary>
        /// Handle server stopped notification
        /// </summary>
        protected virtual void OnStopped() {}

        /// <summary>
        /// Handle session connected notification
        /// </summary>
        /// <param name="session">Connected session</param>
        protected virtual void OnConnected(TcpSession session) {}
        /// <summary>
        /// Handle session disconnected notification
        /// </summary>
        /// <param name="session">Disconnected session</param>
        protected virtual void OnDisconnected(TcpSession session) {}

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError(SocketError error) {}

        internal void OnConnectedInternal(TcpSession session) { OnConnected(session); }
        internal void OnDisconnectedInternal(TcpSession session) { OnDisconnected(session); }

        #endregion
    }
}
