using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace NetCoreServer
{
    /// <summary>
    /// SSL client is used to read/write data from/into the connected SSL server
    /// </summary>
    /// <remarks>Thread-safe</remarks>
    public class SslClient : IDisposable
    {
        /// <summary>
        /// Initialize SSL client with a given server IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public SslClient(SslContext context, IPAddress address, int port) : this(context, new IPEndPoint(address, port)) {}
        /// <summary>
        /// Initialize SSL client with a given server IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public SslClient(SslContext context, string address, int port) : this(context, new IPEndPoint(IPAddress.Parse(address), port)) {}
        /// <summary>
        /// Initialize SSL client with a given DNS endpoint
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="endpoint">DNS endpoint</param>
        public SslClient(SslContext context, DnsEndPoint endpoint) : this(context, endpoint as EndPoint, endpoint.Host, endpoint.Port) {}
        /// <summary>
        /// Initialize SSL client with a given IP endpoint
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="endpoint">IP endpoint</param>
        public SslClient(SslContext context, IPEndPoint endpoint) : this(context, endpoint as EndPoint, endpoint.Address.ToString(), endpoint.Port) {}
        /// <summary>
        /// Initialize SSL client with a given SSL context, endpoint, address and port
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="address">Server address</param>
        /// <param name="port">Server port</param>
        private SslClient(SslContext context, EndPoint endpoint, string address, int port)
        {
            Id = Guid.NewGuid();
            Address = address;
            Port = port;
            Context = context;
            Endpoint = endpoint;
        }

        /// <summary>
        /// Client Id
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// SSL server address
        /// </summary>
        public string Address { get; }
        /// <summary>
        /// SSL server port
        /// </summary>
        public int Port { get; }
        /// <summary>
        /// SSL context
        /// </summary>
        public SslContext Context { get; }
        /// <summary>
        /// Endpoint
        /// </summary>
        public EndPoint Endpoint { get; private set; }
        /// <summary>
        /// Socket
        /// </summary>
        public Socket Socket { get; private set; }

        /// <summary>
        /// Number of bytes pending sent by the client
        /// </summary>
        public long BytesPending { get; private set; }
        /// <summary>
        /// Number of bytes sending by the client
        /// </summary>
        public long BytesSending { get; private set; }
        /// <summary>
        /// Number of bytes sent by the client
        /// </summary>
        public long BytesSent { get; private set; }
        /// <summary>
        /// Number of bytes received by the client
        /// </summary>
        public long BytesReceived { get; private set; }

        /// <summary>
        /// Option: dual mode socket
        /// </summary>
        /// <remarks>
        /// Specifies whether the Socket is a dual-mode socket used for both IPv4 and IPv6.
        /// Will work only if socket is bound on IPv6 address.
        /// </remarks>
        public bool OptionDualMode { get; set; }
        /// <summary>
        /// Option: keep alive
        /// </summary>
        /// <remarks>
        /// This option will setup SO_KEEPALIVE if the OS support this feature
        /// </remarks>
        public bool OptionKeepAlive { get; set; }
        /// <summary>
        /// Option: TCP keep alive time
        /// </summary>
        /// <remarks>
        /// The number of seconds a TCP connection will remain alive/idle before keepalive probes are sent to the remote
        /// </remarks>
        public int OptionTcpKeepAliveTime { get; set; } = -1;
        /// <summary>
        /// Option: TCP keep alive interval
        /// </summary>
        /// <remarks>
        /// The number of seconds a TCP connection will wait for a keepalive response before sending another keepalive probe
        /// </remarks>
        public int OptionTcpKeepAliveInterval { get; set; } = -1;
        /// <summary>
        /// Option: TCP keep alive retry count
        /// </summary>
        /// <remarks>
        /// The number of TCP keep alive probes that will be sent before the connection is terminated
        /// </remarks>
        public int OptionTcpKeepAliveRetryCount { get; set; } = -1;
        /// <summary>
        /// Option: no delay
        /// </summary>
        /// <remarks>
        /// This option will enable/disable Nagle's algorithm for SSL protocol
        /// </remarks>
        public bool OptionNoDelay { get; set; }
        /// <summary>
        /// Option: receive buffer limit
        /// </summary>
        public int OptionReceiveBufferLimit { get; set; } = 0;
        /// <summary>
        /// Option: receive buffer size
        /// </summary>
        public int OptionReceiveBufferSize { get; set; } = 8192;
        /// <summary>
        /// Option: send buffer limit
        /// </summary>
        public int OptionSendBufferLimit { get; set; } = 0;
        /// <summary>
        /// Option: send buffer size
        /// </summary>
        public int OptionSendBufferSize { get; set; } = 8192;

        #region Connect/Disconnect client

        private bool _disconnecting;
        private SocketAsyncEventArgs _connectEventArg;
        private SslStream _sslStream;
        private Guid? _sslStreamId;

        /// <summary>
        /// Is the client connecting?
        /// </summary>
        public bool IsConnecting { get; private set; }
        /// <summary>
        /// Is the client connected?
        /// </summary>
        public bool IsConnected { get; private set; }
        /// <summary>
        /// Is the client handshaking?
        /// </summary>
        public bool IsHandshaking { get; private set; }
        /// <summary>
        /// Is the client handshaked?
        /// </summary>
        public bool IsHandshaked { get; private set; }

        /// <summary>
        /// Create a new socket object
        /// </summary>
        /// <remarks>
        /// Method may be override if you need to prepare some specific socket object in your implementation.
        /// </remarks>
        /// <returns>Socket object</returns>
        protected virtual Socket CreateSocket()
        {
            return new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// Connect the client (synchronous)
        /// </summary>
        /// <remarks>
        /// Please note that synchronous connect will not receive data automatically!
        /// You should use Receive() or ReceiveAsync() method manually after successful connection.
        /// </remarks>
        /// <returns>'true' if the client was successfully connected, 'false' if the client failed to connect</returns>
        public virtual bool Connect()
        {
            if (IsConnected || IsHandshaked || IsConnecting || IsHandshaking)
                return false;

            // Setup buffers
            _receiveBuffer = new Buffer();
            _sendBufferMain = new Buffer();
            _sendBufferFlush = new Buffer();

            // Setup event args
            _connectEventArg = new SocketAsyncEventArgs();
            _connectEventArg.RemoteEndPoint = Endpoint;
            _connectEventArg.Completed += OnAsyncCompleted;

            // Create a new client socket
            Socket = CreateSocket();

            // Update the client socket disposed flag
            IsSocketDisposed = false;

            // Apply the option: dual mode (this option must be applied before connecting)
            if (Socket.AddressFamily == AddressFamily.InterNetworkV6)
                Socket.DualMode = OptionDualMode;

            // Call the client connecting handler
            OnConnecting();

            try
            {
                // Connect to the server
                Socket.Connect(Endpoint);
            }
            catch (SocketException ex)
            {
                // Call the client error handler
                SendError(ex.SocketErrorCode);

                // Reset event args
                _connectEventArg.Completed -= OnAsyncCompleted;

                // Call the client disconnecting handler
                OnDisconnecting();

                // Close the client socket
                Socket.Close();

                // Dispose the client socket
                Socket.Dispose();

                // Dispose event arguments
                _connectEventArg.Dispose();

                // Call the client disconnected handler
                OnDisconnected();

                return false;
            }

            // Apply the option: keep alive
            if (OptionKeepAlive)
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            if (OptionTcpKeepAliveTime >= 0)
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, OptionTcpKeepAliveTime);
            if (OptionTcpKeepAliveInterval >= 0)
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, OptionTcpKeepAliveInterval);
            if (OptionTcpKeepAliveRetryCount >= 0)
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, OptionTcpKeepAliveRetryCount);
            // Apply the option: no delay
            if (OptionNoDelay)
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            // Prepare receive & send buffers
            _receiveBuffer.Reserve(OptionReceiveBufferSize);
            _sendBufferMain.Reserve(OptionSendBufferSize);
            _sendBufferFlush.Reserve(OptionSendBufferSize);

            // Reset statistic
            BytesPending = 0;
            BytesSending = 0;
            BytesSent = 0;
            BytesReceived = 0;

            // Update the connected flag
            IsConnected = true;

            // Call the client connected handler
            OnConnected();

            try
            {
                // Create SSL stream
                _sslStreamId = Guid.NewGuid();
                _sslStream = (Context.CertificateValidationCallback != null) ? new SslStream(new NetworkStream(Socket, false), false, Context.CertificateValidationCallback) : new SslStream(new NetworkStream(Socket, false), false);

                // Call the session handshaking handler
                OnHandshaking();

                // SSL handshake
                if (Context.Certificates != null)
                    _sslStream.AuthenticateAsClient(Address, Context.Certificates, Context.Protocols, true);
                else if (Context.Certificate != null)
                    _sslStream.AuthenticateAsClient(Address, new X509CertificateCollection(new[] { Context.Certificate }), Context.Protocols, true);
                else
                    _sslStream.AuthenticateAsClient(Address);
            }
            catch (Exception)
            {
                SendError(SocketError.NotConnected);
                DisconnectAsync();
                return false;
            }

            // Update the handshaked flag
            IsHandshaked = true;

            // Call the session handshaked handler
            OnHandshaked();

            // Call the empty send buffer handler
            if (_sendBufferMain.IsEmpty)
                OnEmpty();

            return true;
        }

        /// <summary>
        /// Disconnect the client (synchronous)
        /// </summary>
        /// <returns>'true' if the client was successfully disconnected, 'false' if the client is already disconnected</returns>
        public virtual bool Disconnect()
        {
            if (!IsConnected && !IsConnecting)
                return false;

            // Cancel connecting operation
            if (IsConnecting)
                Socket.CancelConnectAsync(_connectEventArg);

            if (_disconnecting)
                return false;

            // Reset connecting & handshaking flags
            IsConnecting = false;
            IsHandshaking = false;

            // Update the disconnecting flag
            _disconnecting = true;

            // Reset event args
            _connectEventArg.Completed -= OnAsyncCompleted;

            // Call the client disconnecting handler
            OnDisconnecting();

            try
            {
                try
                {
                    // Shutdown the SSL stream
                    _sslStream.ShutdownAsync().Wait();
                }
                catch (Exception) {}

                // Dispose the SSL stream & buffer
                _sslStream.Dispose();
                _sslStreamId = null;

                try
                {
                    // Shutdown the socket associated with the client
                    Socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException) {}

                // Close the client socket
                Socket.Close();

                // Dispose the client socket
                Socket.Dispose();

                // Dispose event arguments
                _connectEventArg.Dispose();

                // Update the client socket disposed flag
                IsSocketDisposed = true;
            }
            catch (ObjectDisposedException) {}

            // Update the handshaked flag
            IsHandshaked = false;

            // Update the connected flag
            IsConnected = false;

            // Update sending/receiving flags
            _receiving = false;
            _sending = false;

            // Clear send/receive buffers
            ClearBuffers();

            // Call the client disconnected handler
            OnDisconnected();

            // Reset the disconnecting flag
            _disconnecting = false;

            return true;
        }

        /// <summary>
        /// Reconnect the client (synchronous)
        /// </summary>
        /// <returns>'true' if the client was successfully reconnected, 'false' if the client is already reconnected</returns>
        public virtual bool Reconnect()
        {
            if (!Disconnect())
                return false;

            return Connect();
        }

        /// <summary>
        /// Connect the client (asynchronous)
        /// </summary>
        /// <returns>'true' if the client was successfully connected, 'false' if the client failed to connect</returns>
        public virtual bool ConnectAsync()
        {
            if (IsConnected || IsHandshaked || IsConnecting || IsHandshaking)
                return false;

            // Setup buffers
            _receiveBuffer = new Buffer();
            _sendBufferMain = new Buffer();
            _sendBufferFlush = new Buffer();

            // Setup event args
            _connectEventArg = new SocketAsyncEventArgs();
            _connectEventArg.RemoteEndPoint = Endpoint;
            _connectEventArg.Completed += OnAsyncCompleted;

            // Create a new client socket
            Socket = CreateSocket();

            // Update the client socket disposed flag
            IsSocketDisposed = false;

            // Apply the option: dual mode (this option must be applied before connecting)
            if (Socket.AddressFamily == AddressFamily.InterNetworkV6)
                Socket.DualMode = OptionDualMode;

            // Update the connecting flag
            IsConnecting = true;

            // Call the client connecting handler
            OnConnecting();

            // Async connect to the server
            if (!Socket.ConnectAsync(_connectEventArg))
                ProcessConnect(_connectEventArg);

            return true;
        }

        /// <summary>
        /// Disconnect the client (asynchronous)
        /// </summary>
        /// <returns>'true' if the client was successfully disconnected, 'false' if the client is already disconnected</returns>
        public virtual bool DisconnectAsync() => Disconnect();

        /// <summary>
        /// Reconnect the client (asynchronous)
        /// </summary>
        /// <returns>'true' if the client was successfully reconnected, 'false' if the client is already reconnected</returns>
        public virtual bool ReconnectAsync()
        {
            if (!DisconnectAsync())
                return false;

            while (IsConnected)
                Thread.Yield();

            return ConnectAsync();
        }

        #endregion

        #region Send/Recieve data

        // Receive buffer
        private bool _receiving;
        private Buffer _receiveBuffer;
        // Send buffer
        private readonly object _sendLock = new object();
        private bool _sending;
        private Buffer _sendBufferMain;
        private Buffer _sendBufferFlush;
        private long _sendBufferFlushOffset;

        /// <summary>
        /// Send data to the server (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <returns>Size of sent data</returns>
        public virtual long Send(byte[] buffer) => Send(buffer.AsSpan());

        /// <summary>
        /// Send data to the server (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>Size of sent data</returns>
        public virtual long Send(byte[] buffer, long offset, long size) => Send(buffer.AsSpan((int)offset, (int)size));

        /// <summary>
        /// Send data to the server (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send as a span of bytes</param>
        /// <returns>Size of sent data</returns>
        public virtual long Send(ReadOnlySpan<byte> buffer)
        {
            if (!IsHandshaked)
                return 0;

            if (buffer.IsEmpty)
                return 0;

            try
            {
                // Sent data to the server
                _sslStream.Write(buffer);

                long sent = buffer.Length;

                // Update statistic
                BytesSent += sent;

                // Call the buffer sent handler
                OnSent(sent, BytesPending + BytesSending);

                return sent;
            }
            catch (Exception)
            {
                SendError(SocketError.OperationAborted);
                Disconnect();
                return 0;
            }
        }

        /// <summary>
        /// Send text to the server (synchronous)
        /// </summary>
        /// <param name="text">Text string to send</param>
        /// <returns>Size of sent text</returns>
        public virtual long Send(string text) => Send(Encoding.UTF8.GetBytes(text));

        /// <summary>
        /// Send text to the server (synchronous)
        /// </summary>
        /// <param name="text">Text to send as a span of characters</param>
        /// <returns>Size of sent text</returns>
        public virtual long Send(ReadOnlySpan<char> text) => Send(Encoding.UTF8.GetBytes(text.ToArray()));

        /// <summary>
        /// Send data to the server (asynchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <returns>'true' if the data was successfully sent, 'false' if the client is not connected</returns>
        public virtual bool SendAsync(byte[] buffer) => SendAsync(buffer.AsSpan());

        /// <summary>
        /// Send data to the server (asynchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>'true' if the data was successfully sent, 'false' if the client is not connected</returns>
        public virtual bool SendAsync(byte[] buffer, long offset, long size) => SendAsync(buffer.AsSpan((int)offset, (int)size));

        /// <summary>
        /// Send data to the server (asynchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send as a span of bytes</param>
        /// <returns>'true' if the data was successfully sent, 'false' if the client is not connected</returns>
        public virtual bool SendAsync(ReadOnlySpan<byte> buffer)
        {
            if (!IsHandshaked)
                return false;

            if (buffer.IsEmpty)
                return true;

            lock (_sendLock)
            {
                // Check the send buffer limit
                if (((_sendBufferMain.Size + buffer.Length) > OptionSendBufferLimit) && (OptionSendBufferLimit > 0))
                {
                    SendError(SocketError.NoBufferSpaceAvailable);
                    return false;
                }

                // Fill the main send buffer
                _sendBufferMain.Append(buffer);

                // Update statistic
                BytesPending = _sendBufferMain.Size;

                // Avoid multiple send handlers
                if (_sending)
                    return true;
                else
                    _sending = true;

                // Try to send the main buffer
                TrySend();
            }

            return true;
        }

        /// <summary>
        /// Send text to the server (asynchronous)
        /// </summary>
        /// <param name="text">Text string to send</param>
        /// <returns>'true' if the text was successfully sent, 'false' if the client is not connected</returns>
        public virtual bool SendAsync(string text) => SendAsync(Encoding.UTF8.GetBytes(text));

        /// <summary>
        /// Send text to the server (asynchronous)
        /// </summary>
        /// <param name="text">Text to send as a span of characters</param>
        /// <returns>'true' if the text was successfully sent, 'false' if the client is not connected</returns>
        public virtual bool SendAsync(ReadOnlySpan<char> text) => SendAsync(Encoding.UTF8.GetBytes(text.ToArray()));

        /// <summary>
        /// Receive data from the server (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to receive</param>
        /// <returns>Size of received data</returns>
        public virtual long Receive(byte[] buffer) { return Receive(buffer, 0, buffer.Length); }

        /// <summary>
        /// Receive data from the server (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to receive</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>Size of received data</returns>
        public virtual long Receive(byte[] buffer, long offset, long size)
        {
            if (!IsHandshaked)
                return 0;

            if (size == 0)
                return 0;

            try
            {
                // Receive data from the server
                long received = _sslStream.Read(buffer, (int)offset, (int)size);
                if (received > 0)
                {
                    // Update statistic
                    BytesReceived += received;

                    // Call the buffer received handler
                    OnReceived(buffer, 0, received);
                }

                return received;
            }
            catch (Exception)
            {
                SendError(SocketError.OperationAborted);
                Disconnect();
                return 0;
            }
        }

        /// <summary>
        /// Receive text from the server (synchronous)
        /// </summary>
        /// <param name="size">Text size to receive</param>
        /// <returns>Received text</returns>
        public virtual string Receive(long size)
        {
            var buffer = new byte[size];
            var length = Receive(buffer);
            return Encoding.UTF8.GetString(buffer, 0, (int)length);
        }

        /// <summary>
        /// Receive data from the server (asynchronous)
        /// </summary>
        public virtual void ReceiveAsync()
        {
            // Try to receive data from the server
            TryReceive();
        }

        /// <summary>
        /// Try to receive new data
        /// </summary>
        private void TryReceive()
        {
            if (_receiving)
                return;

            if (!IsHandshaked)
                return;

            try
            {
                // Async receive with the receive handler
                IAsyncResult result;
                do
                {
                    if (!IsHandshaked)
                        return;

                    _receiving = true;
                    result = _sslStream.BeginRead(_receiveBuffer.Data, 0, (int)_receiveBuffer.Capacity, ProcessReceive, _sslStreamId);
                } while (result.CompletedSynchronously);

            }
            catch (ObjectDisposedException) {}
        }

        /// <summary>
        /// Try to send pending data
        /// </summary>
        private void TrySend()
        {
            if (!IsHandshaked)
                return;

            bool empty = false;

            lock (_sendLock)
            {
                // Is previous socket send in progress?
                if (_sendBufferFlush.IsEmpty)
                {
                    // Swap flush and main buffers
                    _sendBufferFlush = Interlocked.Exchange(ref _sendBufferMain, _sendBufferFlush);
                    _sendBufferFlushOffset = 0;

                    // Update statistic
                    BytesPending = 0;
                    BytesSending += _sendBufferFlush.Size;

                    // Check if the flush buffer is empty
                    if (_sendBufferFlush.IsEmpty)
                    {
                        // Need to call empty send buffer handler
                        empty = true;

                        // End sending process
                        _sending = false;
                    }
                }
                else
                    return;
            }

            // Call the empty send buffer handler
            if (empty)
            {
                OnEmpty();
                return;
            }

            try
            {
                // Async write with the write handler
                _sslStream.BeginWrite(_sendBufferFlush.Data, (int)_sendBufferFlushOffset, (int)(_sendBufferFlush.Size - _sendBufferFlushOffset), ProcessSend, _sslStreamId);
            }
            catch (ObjectDisposedException) {}
        }

        /// <summary>
        /// Clear send/receive buffers
        /// </summary>
        private void ClearBuffers()
        {
            lock (_sendLock)
            {
                // Clear send buffers
                _sendBufferMain.Clear();
                _sendBufferFlush.Clear();
                _sendBufferFlushOffset= 0;

                // Update statistic
                BytesPending = 0;
                BytesSending = 0;
            }
        }

        #endregion

        #region IO processing

        /// <summary>
        /// This method is called whenever a receive or send operation is completed on a socket
        /// </summary>
        private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (IsSocketDisposed)
                return;

            // Determine which type of operation just completed and call the associated handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    ProcessConnect(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }

        }

        /// <summary>
        /// This method is invoked when an asynchronous connect operation completes
        /// </summary>
        private void ProcessConnect(SocketAsyncEventArgs e)
        {
            IsConnecting = false;

            if (e.SocketError == SocketError.Success)
            {
                // Apply the option: keep alive
                if (OptionKeepAlive)
                    Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                if (OptionTcpKeepAliveTime >= 0)
                    Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, OptionTcpKeepAliveTime);
                if (OptionTcpKeepAliveInterval >= 0)
                    Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, OptionTcpKeepAliveInterval);
                if (OptionTcpKeepAliveRetryCount >= 0)
                    Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, OptionTcpKeepAliveRetryCount);
                // Apply the option: no delay
                if (OptionNoDelay)
                    Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                // Prepare receive & send buffers
                _receiveBuffer.Reserve(OptionReceiveBufferSize);
                _sendBufferMain.Reserve(OptionSendBufferSize);
                _sendBufferFlush.Reserve(OptionSendBufferSize);

                // Reset statistic
                BytesPending = 0;
                BytesSending = 0;
                BytesSent = 0;
                BytesReceived = 0;

                // Update the connected flag
                IsConnected = true;

                // Call the client connected handler
                OnConnected();

                try
                {
                    // Create SSL stream
                    _sslStreamId = Guid.NewGuid();
                    _sslStream = (Context.CertificateValidationCallback != null) ? new SslStream(new NetworkStream(Socket, false), false, Context.CertificateValidationCallback) : new SslStream(new NetworkStream(Socket, false), false);

                    // Call the session handshaking handler
                    OnHandshaking();

                    // Begin the SSL handshake
                    IsHandshaking = true;
                    if (Context.Certificates != null)
                        _sslStream.BeginAuthenticateAsClient(Address, Context.Certificates, Context.Protocols, true, ProcessHandshake, _sslStreamId);
                    else if (Context.Certificate != null)
                        _sslStream.BeginAuthenticateAsClient(Address, new X509CertificateCollection(new[] { Context.Certificate }), Context.Protocols, true, ProcessHandshake, _sslStreamId);
                    else
                        _sslStream.BeginAuthenticateAsClient(Address, ProcessHandshake, _sslStreamId);
                }
                catch (Exception)
                {
                    SendError(SocketError.NotConnected);
                    DisconnectAsync();
                }
            }
            else
            {
                // Call the client disconnected handler
                SendError(e.SocketError);
                OnDisconnected();
            }
        }

        /// <summary>
        /// This method is invoked when an asynchronous handshake operation completes
        /// </summary>
        private void ProcessHandshake(IAsyncResult result)
        {
            try
            {
                IsHandshaking = false;

                if (IsHandshaked)
                    return;

                // Validate SSL stream Id
                var sslStreamId = result.AsyncState as Guid?;
                if (_sslStreamId != sslStreamId)
                    return;

                // End the SSL handshake
                _sslStream.EndAuthenticateAsClient(result);

                // Update the handshaked flag
                IsHandshaked = true;

                // Try to receive something from the server
                TryReceive();

                // Check the socket disposed state: in some rare cases it might be disconnected while receiving!
                if (IsSocketDisposed)
                    return;

                // Call the session handshaked handler
                OnHandshaked();

                // Call the empty send buffer handler
                if (_sendBufferMain.IsEmpty)
                    OnEmpty();
            }
            catch (Exception)
            {
                SendError(SocketError.NotConnected);
                DisconnectAsync();
            }
        }

        /// <summary>
        /// This method is invoked when an asynchronous receive operation completes
        /// </summary>
        private void ProcessReceive(IAsyncResult result)
        {
            try
            {
                if (!IsHandshaked)
                    return;

                // Validate SSL stream Id
                var sslStreamId = result.AsyncState as Guid?;
                if (_sslStreamId != sslStreamId)
                    return;

                // End the SSL read
                long size = _sslStream.EndRead(result);

                // Received some data from the server
                if (size > 0)
                {
                    // Update statistic
                    BytesReceived += size;

                    // Call the buffer received handler
                    OnReceived(_receiveBuffer.Data, 0, size);

                    // If the receive buffer is full increase its size
                    if (_receiveBuffer.Capacity == size)
                    {
                        // Check the receive buffer limit
                        if (((2 * size) > OptionReceiveBufferLimit) && (OptionReceiveBufferLimit > 0))
                        {
                            SendError(SocketError.NoBufferSpaceAvailable);
                            DisconnectAsync();
                            return;
                        }

                        _receiveBuffer.Reserve(2 * size);
                    }
                }

                _receiving = false;

                // If zero is returned from a read operation, the remote end has closed the connection
                if (size > 0)
                {
                    if (!result.CompletedSynchronously)
                        TryReceive();
                }
                else
                    DisconnectAsync();
            }
            catch (Exception)
            {
                SendError(SocketError.OperationAborted);
                DisconnectAsync();
            }
        }

        /// <summary>
        /// This method is invoked when an asynchronous send operation completes
        /// </summary>
        private void ProcessSend(IAsyncResult result)
        {
            try
            {
                if (!IsHandshaked)
                    return;

                // Validate SSL stream Id
                var sslStreamId = result.AsyncState as Guid?;
                if (_sslStreamId != sslStreamId)
                    return;

                // End the SSL write
                _sslStream.EndWrite(result);

                long size = _sendBufferFlush.Size;

                // Send some data to the server
                if (size > 0)
                {
                    // Update statistic
                    BytesSending -= size;
                    BytesSent += size;

                    // Increase the flush buffer offset
                    _sendBufferFlushOffset += size;

                    // Successfully send the whole flush buffer
                    if (_sendBufferFlushOffset == _sendBufferFlush.Size)
                    {
                        // Clear the flush buffer
                        _sendBufferFlush.Clear();
                        _sendBufferFlushOffset = 0;
                    }

                    // Call the buffer sent handler
                    OnSent(size, BytesPending + BytesSending);
                }

                // Try to send again if the client is valid
                TrySend();
            }
            catch (Exception)
            {
                SendError(SocketError.OperationAborted);
                DisconnectAsync();
            }
        }

        #endregion

        #region Session handlers

        /// <summary>
        /// Handle client connecting notification
        /// </summary>
        protected virtual void OnConnecting() {}
        /// <summary>
        /// Handle client connected notification
        /// </summary>
        protected virtual void OnConnected() {}
        /// <summary>
        /// Handle client handshaking notification
        /// </summary>
        protected virtual void OnHandshaking() {}
        /// <summary>
        /// Handle client handshaked notification
        /// </summary>
        protected virtual void OnHandshaked() {}
        /// <summary>
        /// Handle client disconnecting notification
        /// </summary>
        protected virtual void OnDisconnecting() {}
        /// <summary>
        /// Handle client disconnected notification
        /// </summary>
        protected virtual void OnDisconnected() {}

        /// <summary>
        /// Handle buffer received notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        /// <remarks>
        /// Notification is called when another chunk of buffer was received from the server
        /// </remarks>
        protected virtual void OnReceived(byte[] buffer, long offset, long size) {}
        /// <summary>
        /// Handle buffer sent notification
        /// </summary>
        /// <param name="sent">Size of sent buffer</param>
        /// <param name="pending">Size of pending buffer</param>
        /// <remarks>
        /// Notification is called when another chunk of buffer was sent to the server.
        /// This handler could be used to send another buffer to the server for instance when the pending size is zero.
        /// </remarks>
        protected virtual void OnSent(long sent, long pending) {}

        /// <summary>
        /// Handle empty send buffer notification
        /// </summary>
        /// <remarks>
        /// Notification is called when the send buffer is empty and ready for a new data to send.
        /// This handler could be used to send another buffer to the server.
        /// </remarks>
        protected virtual void OnEmpty() {}

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError(SocketError error) {}

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
        /// Client socket disposed flag
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
                    DisconnectAsync();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                IsDisposed = true;
            }
        }

        #endregion
    }
}
