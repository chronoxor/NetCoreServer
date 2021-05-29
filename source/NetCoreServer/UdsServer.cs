using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetCoreServer
{
    public class UdsServer : IDisposable
    {
        /// <summary>
        /// Initialize Uds(Unix Domain Socket) server
        /// </summary>
        /// <param name="path">The path to connect a unix domain socket over</param>
        public UdsServer(string path)
        {
            Patch = path;
        }

        /// <summary>
        /// The path to connect a unix domain socket over
        /// </summary>
        public string Patch { get; }

        /// <summary>
        /// Unix domain socket endpoint
        /// </summary>
        public UnixDomainSocketEndPoint EndPoint { get; private set; }

        /// <summary>
        /// Option: acceptor backlog size
        /// </summary>
        /// <remarks>
        /// This option will set the listening socket's backlog size
        /// </remarks>
        public int OptionAcceptorBacklog { get; set; } = 1024;

        /// <summary>
        /// Option: receive buffer size
        /// </summary>
        public int OptionReceiveBufferSize { get; set; } = 8192;
        /// <summary>
        /// Option: send buffer size
        /// </summary>
        public int OptionSendBufferSize { get; set; } = 8192;

        #region Start/Stop server

        // Server acceptor
        private Socket _acceptorSocket;
        private SocketAsyncEventArgs _acceptorEventArg;

        // Server statistic
        internal long _bytesPending;
        internal long _bytesSent;
        internal long _bytesReceived;

        /// <summary>
        /// Is the server started?
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Is the server accepting new clients?
        /// </summary>
        public bool IsAccepting { get; private set; }

        /// <summary>
        /// Create a new socket object
        /// </summary>
        /// <remarks>
        /// Method may be override if you need to prepare some specific socket object in your implementation.
        /// </remarks>
        /// <returns>Socket object</returns>
        protected virtual Socket CreateSocket()
        {
            return new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        }

        /// <summary>
        /// Start the server
        /// </summary>
        /// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
        public virtual bool Start()
        {
            Debug.Assert(!IsStarted, "UDS server is already started!");
            if (IsStarted)
                return false;

            // Setup acceptor event arg
            _acceptorEventArg = new SocketAsyncEventArgs();
            _acceptorEventArg.Completed += OnAsyncCompleted;

            // Create a new acceptor socket
            _acceptorSocket = CreateSocket();

            // Update the acceptor socket disposed flag
            IsSocketDisposed = false;

            if (System.IO.File.Exists(Patch))
                System.IO.File.Delete(Patch);

            EndPoint = new UnixDomainSocketEndPoint(Patch);

            // Bind the acceptor socket to the IP endpoint
            _acceptorSocket.Bind(EndPoint);

            // Call the server starting handler
            OnStarting();

            _acceptorSocket.Listen(OptionAcceptorBacklog);

            // Reset statistic
            _bytesPending = 0;
            _bytesSent = 0;
            _bytesReceived = 0;

            // Update the started flag
            IsStarted = true;

            // Call the server started handler
            OnStarted();

            // Perform the first server accept
            IsAccepting = true;
            StartAccept(_acceptorEventArg);

            return true;
        }

        /// <summary>
        /// Stop the server
        /// </summary>
        /// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
        public virtual bool Stop()
        {
            Debug.Assert(IsStarted, "UDS server is not started!");
            if (!IsStarted)
                return false;

            // Stop accepting new clients
            IsAccepting = false;

            // Reset acceptor event arg
            _acceptorEventArg.Completed -= OnAsyncCompleted;

            // Call the server stopping handler
            OnStopping();

            try
            {
                // Close the acceptor socket
                _acceptorSocket.Close();

                // Dispose the acceptor socket
                _acceptorSocket.Dispose();

                // Dispose event arguments
                _acceptorEventArg.Dispose();

                // Update the acceptor socket disposed flag
                IsSocketDisposed = true;
            }
            catch (ObjectDisposedException) { }

            // Disconnect all sessions
            DisconnectAll();

            // Update the started flag
            IsStarted = false;

            // Call the server stopped handler
            OnStopped();

            return true;
        }

        /// <summary>
        /// Restart the server
        /// </summary>
        /// <returns>'true' if the server was successfully restarted, 'false' if the server failed to restart</returns>
        public virtual bool Restart()
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

            // Async accept a new client connection
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
                SendError(e.SocketError);

            // Accept the next client connection
            if (IsAccepting)
                StartAccept(e);
        }

        /// <summary>
        /// This method is the callback method associated with Socket.AcceptAsync()
        /// operations and is invoked when an accept operation is complete
        /// </summary>
        private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (IsSocketDisposed)
                return;

            ProcessAccept(e);
        }

        #endregion

        #region Session factory

        /// <summary>
        /// Create UDS session factory method
        /// </summary>
        /// <returns>UDS session</returns>
        protected virtual UdsSession CreateSession() { return new UdsSession(this); }

        #endregion

        #region Server handlers

        /// <summary>
        /// Handle server starting notification
        /// </summary>
        protected virtual void OnStarting() { }

        /// <summary>
        /// Handle server started notification
        /// </summary>
        protected virtual void OnStarted() { }

        /// <summary>
        /// Handle server stopping notification
        /// </summary>
        protected virtual void OnStopping() { }

        /// <summary>
        /// Handle server stopped notification
        /// </summary>
        protected virtual void OnStopped() { }

        /// <summary>
        /// Handle session connecting notification
        /// </summary>
        /// <param name="session">Connecting session</param>
        protected virtual void OnConnecting(UdsSession session) { }
        
        /// <summary>
        /// Handle session connected notification
        /// </summary>
        /// <param name="session">Connected session</param>
        protected virtual void OnConnected(UdsSession session) { }
        
        /// <summary>
        /// Handle session disconnecting notification
        /// </summary>
        /// <param name="session">Disconnecting session</param>
        protected virtual void OnDisconnecting(UdsSession session) { }
        
        /// <summary>
        /// Handle session disconnected notification
        /// </summary>
        /// <param name="session">Disconnected session</param>
        protected virtual void OnDisconnected(UdsSession session) { }

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError(SocketError error) { }

        internal void OnConnectingInternal(UdsSession session) { OnConnecting(session); }

        internal void OnConnectedInternal(UdsSession session) { OnConnected(session); }

        internal void OnDisconnectingInternal(UdsSession session) { OnDisconnecting(session); }

        internal void OnDisconnectedInternal(UdsSession session) { OnDisconnected(session); }

        #endregion

        #region Session management

        // Server sessions
        protected readonly ConcurrentDictionary<Guid, UdsSession> Sessions = new();

        /// <summary>
        /// Disconnect all connected sessions
        /// </summary>
        /// <returns>'true' if all sessions were successfully disconnected, 'false' if the server is not started</returns>
        public virtual bool DisconnectAll()
        {
            if (!IsStarted)
                return false;

            // Disconnect all sessions
            foreach (var session in Sessions.Values)
                session.Disconnect();

            return true;
        }

        /// <summary>
        /// Find a session with a given Id
        /// </summary>
        /// <param name="id">Session Id</param>
        /// <returns>Session with a given Id or null if the session it not connected</returns>
        public UdsSession FindSession(Guid id)
        {
            // Try to find the required session
            return Sessions.TryGetValue(id, out UdsSession result) ? result : null;
        }

        /// <summary>
        /// Register a new session
        /// </summary>
        /// <param name="session">Session to register</param>
        internal void RegisterSession(UdsSession session)
        {
            // Register a new session
            Sessions.TryAdd(session.Id, session);
        }

        /// <summary>
        /// Unregister session by Id
        /// </summary>
        /// <param name="id">Session Id</param>
        internal void UnregisterSession(Guid id)
        {
            // Unregister session by Id
            Sessions.TryRemove(id, out UdsSession temp);
        }

        #endregion

        #region Multicasting

        /// <summary>
        /// Multicast data to all connected sessions
        /// </summary>
        /// <param name="buffer">Buffer to multicast</param>
        /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
        public virtual bool Multicast(byte[] buffer) { return Multicast(buffer, 0, buffer.Length); }

        /// <summary>
        /// Multicast data to all connected clients
        /// </summary>
        /// <param name="buffer">Buffer to multicast</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
        public virtual bool Multicast(byte[] buffer, long offset, long size)
        {
            if (!IsStarted)
                return false;

            if (size == 0)
                return true;

            // Multicast data to all sessions
            foreach (var session in Sessions.Values)
                session.SendAsync(buffer, offset, size);

            return true;
        }

        /// <summary>
        /// Multicast text to all connected clients
        /// </summary>
        /// <param name="text">Text string to multicast</param>
        /// <returns>'true' if the text was successfully multicasted, 'false' if the text was not multicasted</returns>
        public virtual bool Multicast(string text) { return Multicast(Encoding.UTF8.GetBytes(text)); }

        #endregion

        #region Error handling

        /// <summary>
        /// Send error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        private void SendError(SocketError error)
        {
            // Skip disconnect errors
            if ((error == SocketError.ConnectionAborted) ||
                (error == SocketError.ConnectionRefused) ||
                (error == SocketError.ConnectionReset) ||
                (error == SocketError.OperationAborted) ||
                (error == SocketError.Shutdown))
                return;

            OnError(error);
        }

        #endregion

        #region IDisposable implementation

        /// <summary>
        /// Disposed flag
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Acceptor socket disposed flag
        /// </summary>
        public bool IsSocketDisposed { get; private set; } = true;

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

            if (!IsDisposed)
            {
                if (disposingManagedResources)
                {
                    // Dispose managed resources here...
                    Stop();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                IsDisposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~UdsServer()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        #endregion
    }
}