using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NETCoreServer
{
    /// <summary>
    /// TCP server is used to connect, disconnect and manage TCP sessions
    /// </summary>
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
        public IPEndPoint Endpoint { get; internal set; }

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

        /// <summary>
        /// Handle server started notification
        /// </summary>
        protected virtual void OnStarted() {}
        /// <summary>
        /// Handle server stopped notification
        /// </summary>
        protected virtual void OnStopped() {}

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError(SocketError error) {}

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

        #region Multicasting

        /// <summary>
        /// Clear multicast buffer
        /// </summary>
        private void ClearBuffers()
        {

        }

        #endregion

        #region Session management

        /// <summary>
        /// Disconnect all connected sessions
        /// </summary>
        /// <returns>'true' if all sessions were successfully disconnected, 'false' if the server is not started</returns>
        public bool DisconnectAll()
        {
            return true;
        }

        #endregion
    }
}
