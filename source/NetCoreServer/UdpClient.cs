using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetCoreServer
{
    /// <summary>
    /// UDP client is used to read/write data from/into the connected UDP server
    /// </summary>
    /// <remarks>Thread-safe</remarks>
    public class UdpClient : IDisposable
    {
        /// <summary>
        /// Initialize UDP client with a given server IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public UdpClient(IPAddress address, int port) : this(new IPEndPoint(address, port)) {}
        /// <summary>
        /// Initialize UDP client with a given server IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public UdpClient(string address, int port) : this(new IPEndPoint(IPAddress.Parse(address), port)) {}
        /// <summary>
        /// Initialize UDP client with a given IP endpoint
        /// </summary>
        /// <param name="endpoint">IP endpoint</param>
        public UdpClient(IPEndPoint endpoint)
        {
            Id = Guid.NewGuid();
            Endpoint = endpoint;
        }

        /// <summary>
        /// TCP client Id
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// IP endpoint
        /// </summary>
        public IPEndPoint Endpoint { get; private set; }
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
        /// Number of datagrams sent by the client
        /// </summary>
        public long DatagramsSent { get; private set; }
        /// <summary>
        /// Number of datagrams received by the client
        /// </summary>
        public long DatagramsReceived { get; private set; }

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
        /// Option: bind the socket to the multicast UDP server
        /// </summary>
        public bool OptionMulticast { get; set; }
        /// <summary>
        /// Option: receive buffer size
        /// </summary>
        public int OptionReceiveBufferSize
        {
            get => Socket.ReceiveBufferSize;
            set => Socket.ReceiveBufferSize = value;
        }
        /// <summary>
        /// Option: send buffer size
        /// </summary>
        public int OptionSendBufferSize
        {
            get => Socket.SendBufferSize;
            set => Socket.SendBufferSize = value;
        }

        #region Connect/Disconnect client

        /// <summary>
        /// Is the client connected?
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Connect the client
        /// </summary>
        /// <returns>'true' if the client was successfully connected, 'false' if the client failed to connect</returns>
        public virtual bool Connect()
        {
            if (IsConnected)
                return false;

            // Setup event args
            _receiveEventArg.Completed += OnAsyncCompleted;
            _sendEventArg.Completed += OnAsyncCompleted;

            // Create a new client socket
            Socket = new Socket(Endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            // Apply the option: reuse address
            if (OptionReuseAddress)
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // Apply the option: reuse port
            /*
            if (OptionReusePort)
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReusePort, true);
            */

            // Bind the acceptor socket to the IP endpoint
            if (OptionMulticast)
                Socket.Bind(Endpoint);
            else
            {
                var endpoint = new IPEndPoint((Endpoint.AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any: IPAddress.Any, 0);
                Socket.Bind(endpoint);
            }

            // Prepare receive endpoint
            _receiveEndpoint = new IPEndPoint((Endpoint.AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any : IPAddress.Any, 0);

            // Prepare receive & send buffers
            _receiveBuffer.Reserve(OptionReceiveBufferSize);

            // Reset statistic
            BytesPending = 0;
            BytesSending = 0;
            BytesSent = 0;
            BytesReceived = 0;
            DatagramsSent = 0;
            DatagramsReceived = 0;

            // Update the connected flag
            IsConnected = true;

            // Call the client connected handler
            OnConnected();

            return true;
        }

        /// <summary>
        /// Disconnect the client
        /// </summary>
        /// <returns>'true' if the client was successfully disconnected, 'false' if the client is already disconnected</returns>
        public virtual bool Disconnect()
        {
            if (!IsConnected)
                return false;

            // Reset event args
            _receiveEventArg.Completed -= OnAsyncCompleted;
            _sendEventArg.Completed -= OnAsyncCompleted;

            try
            {
                // Shutdown the socket associated with the client
                Socket.Shutdown(SocketShutdown.Both);

                // Close the client socket
                Socket.Close();

                // Dispose the client socket
                Socket.Dispose();
            }
            catch (ObjectDisposedException) { }

            // Update the connected flag
            IsConnected = false;

            // Update sending/receiving flags
            _receiving = false;
            _sending = false;

            // Clear send/receive buffers
            ClearBuffers();

            // Call the client disconnected handler
            OnDisconnected();

            return true;
        }

        /// <summary>
        /// Reconnect the client
        /// </summary>
        /// <returns>'true' if the client was successfully reconnected, 'false' if the client is already reconnected</returns>
        public virtual bool Reconnect()
        {
            if (!Disconnect())
                return false;

            while (IsConnected)
                Thread.Yield();

            return Connect();
        }

        #endregion

        #region Multicast group

        /// <summary>
        /// Setup multicast: bind the socket to the multicast UDP server
        /// </summary>
        /// <param name="enable">Enable/disable multicast</param>
        public virtual void SetupMulticast(bool enable)
        {
            OptionReuseAddress = enable;
            OptionMulticast = enable;
        }

        /// <summary>
        /// Join multicast group with a given IP address
        /// </summary>
        /// <param name="address">IP address</param>
        public virtual void JoinMulticastGroup(IPAddress address)
        {
            var join = new MulticastOption(address, Endpoint.Address);
            Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, join);

            // Call the client joined multicast group notification
            OnJoinedMulticastGroup(address);
        }
        /// <summary>
        /// Join multicast group with a given IP address
        /// </summary>
        /// <param name="address">IP address</param>
        public virtual void JoinMulticastGroup(string address) { JoinMulticastGroup(IPAddress.Parse(address)); }

        /// <summary>
        /// Leave multicast group with a given IP address
        /// </summary>
        /// <param name="address">IP address</param>
        public virtual void LeaveMulticastGroup(IPAddress address)
        {
            var leave = new MulticastOption(address, Endpoint.Address);
            Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, leave);

            // Call the client left multicast group notification
            OnLeftMulticastGroup(address);
        }
        /// <summary>
        /// Leave multicast group with a given IP address
        /// </summary>
        /// <param name="address">IP address</param>
        public virtual void LeaveMulticastGroup(string address) { LeaveMulticastGroup(IPAddress.Parse(address)); }

        #endregion

        #region Send/Recieve data

        // Receive and send endpoints
        IPEndPoint _receiveEndpoint;
        IPEndPoint _sendEndpoint;
        // Receive buffer
        private bool _receiving;
        private readonly Buffer _receiveBuffer = new Buffer();
        private readonly SocketAsyncEventArgs _receiveEventArg = new SocketAsyncEventArgs();
        // Send buffer
        private bool _sending;
        private Buffer _sendBuffer = new Buffer();
        private readonly SocketAsyncEventArgs _sendEventArg = new SocketAsyncEventArgs();

        /// <summary>
        /// Receive a new datagram
        /// </summary>
        public virtual void Receive() { TryReceive(); }

        /// <summary>
        /// Send datagram to the connected server (asynchronous)
        /// </summary>
        /// <param name="buffer">Datagram buffer to send</param>
        /// <returns>'true' if the datagram was successfully sent, 'false' if the datagram was not sent</returns>
        public virtual bool SendAsync(byte[] buffer) { return SendAsync(buffer, 0, buffer.Length); }

        /// <summary>
        /// Send datagram to the connected server (asynchronous)
        /// </summary>
        /// <param name="buffer">Datagram buffer to send</param>
        /// <param name="offset">Datagram buffer offset</param>
        /// <param name="size">Datagram buffer size</param>
        /// <returns>'true' if the datagram was successfully sent, 'false' if the datagram was not sent</returns>
        public virtual bool SendAsync(byte[] buffer, long offset, long size) { return SendAsync(Endpoint, buffer, offset, size); }

        /// <summary>
        /// Send text to the connected server (asynchronous)
        /// </summary>
        /// <param name="text">Text string to send</param>
        /// <returns>'true' if the text was successfully sent, 'false' if the text was not sent</returns>
        public virtual bool SendAsync(string text) { return SendAsync(Encoding.UTF8.GetBytes(text)); }

        /// <summary>
        /// Send datagram to the given endpoint (asynchronous)
        /// </summary>
        /// <param name="endpoint">Endpoint to send</param>
        /// <param name="buffer">Datagram buffer to send</param>
        /// <returns>'true' if the datagram was successfully sent, 'false' if the datagram was not sent</returns>
        public virtual bool SendAsync(IPEndPoint endpoint, byte[] buffer) { return SendAsync(endpoint, buffer, 0, buffer.Length); }

        /// <summary>
        /// Send datagram to the given endpoint (asynchronous)
        /// </summary>
        /// <param name="endpoint">Endpoint to send</param>
        /// <param name="buffer">Datagram buffer to send</param>
        /// <param name="offset">Datagram buffer offset</param>
        /// <param name="size">Datagram buffer size</param>
        /// <returns>'true' if the datagram was successfully sent, 'false' if the datagram was not sent</returns>
        public virtual bool SendAsync(IPEndPoint endpoint, byte[] buffer, long offset, long size)
        {
            if (_sending)
                return false;

            if (!IsConnected)
                return false;

            if (size == 0)
                return true;

            // Fill the main send buffer
            _sendBuffer.Append(buffer, offset, size);

            // Update statistic
            BytesSending = _sendBuffer.Size;

            // Update send endpoint
            _sendEndpoint = endpoint;

            // Try to send the main buffer
            TrySend();

            return true;
        }

        /// <summary>
        /// Send text to the given endpoint (asynchronous)
        /// </summary>
        /// <param name="endpoint">Endpoint to send</param>
        /// <param name="text">Text string to send</param>
        /// <returns>'true' if the text was successfully sent, 'false' if the text was not sent</returns>
        public virtual bool SendAsync(IPEndPoint endpoint, string text) { return SendAsync(endpoint, Encoding.UTF8.GetBytes(text)); }

        /// <summary>
        /// Send datagram to the connected server (synchronous)
        /// </summary>
        /// <param name="buffer">Datagram buffer to send</param>
        /// <returns>'true' if the datagram was successfully sent, 'false' if the datagram was not sent</returns>
        public virtual bool SendSync(byte[] buffer) { return SendSync(buffer, 0, buffer.Length); }

        /// <summary>
        /// Send datagram to the connected server (synchronous)
        /// </summary>
        /// <param name="buffer">Datagram buffer to send</param>
        /// <param name="offset">Datagram buffer offset</param>
        /// <param name="size">Datagram buffer size</param>
        /// <returns>'true' if the datagram was successfully sent, 'false' if the datagram was not sent</returns>
        public virtual bool SendSync(byte[] buffer, long offset, long size) { return SendSync(Endpoint, buffer, offset, size); }

        /// <summary>
        /// Send text to the connected server (synchronous)
        /// </summary>
        /// <param name="text">Text string to send</param>
        /// <returns>'true' if the text was successfully sent, 'false' if the text was not sent</returns>
        public virtual bool SendSync(string text) { return SendSync(Encoding.UTF8.GetBytes(text)); }

        /// <summary>
        /// Send datagram to the given endpoint (synchronous)
        /// </summary>
        /// <param name="endpoint">Endpoint to send</param>
        /// <param name="buffer">Datagram buffer to send</param>
        /// <returns>'true' if the datagram was successfully sent, 'false' if the datagram was not sent</returns>
        public virtual bool SendSync(IPEndPoint endpoint, byte[] buffer) { return SendSync(endpoint, buffer, 0, buffer.Length); }

        /// <summary>
        /// Send datagram to the given endpoint (synchronous)
        /// </summary>
        /// <param name="endpoint">Endpoint to send</param>
        /// <param name="buffer">Datagram buffer to send</param>
        /// <param name="offset">Datagram buffer offset</param>
        /// <param name="size">Datagram buffer size</param>
        /// <returns>'true' if the datagram was successfully sent, 'false' if the datagram was not sent</returns>
        public virtual bool SendSync(IPEndPoint endpoint, byte[] buffer, long offset, long size)
        {
            if (!IsConnected)
                return false;

            if (size == 0)
                return true;

            try
            {
                // Sent datagram to the server
                int sent = Socket.SendTo(buffer, (int)offset, (int)size, SocketFlags.None, endpoint);
                if (sent > 0)
                {
                    // Update statistic
                    DatagramsSent++;
                    BytesSent += sent;

                    // Call the datagram sent handler
                    OnSent(endpoint, sent);
                }

                return true;
            }
            catch (ObjectDisposedException) { return false; }
            catch (SocketException ex)
            {
                SendError(ex.SocketErrorCode);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Send text to the given endpoint (synchronous)
        /// </summary>
        /// <param name="endpoint">Endpoint to send</param>
        /// <param name="text">Text string to send</param>
        /// <returns>'true' if the text was successfully sent, 'false' if the text was not sent</returns>
        public virtual bool SendSync(IPEndPoint endpoint, string text) { return SendSync(endpoint, Encoding.UTF8.GetBytes(text)); }

        /// <summary>
        /// Try to receive new data
        /// </summary>
        private void TryReceive()
        {
            if (_receiving)
                return;

            if (!IsConnected)
                return;

            try
            {
                // Async receive with the receive handler
                _receiving = true;
                _receiveEventArg.RemoteEndPoint = _receiveEndpoint;
                _receiveEventArg.SetBuffer(_receiveBuffer.Data, 0, (int)_receiveBuffer.Capacity);
                if (!Socket.ReceiveFromAsync(_receiveEventArg))
                    ProcessReceiveFrom(_receiveEventArg);
            }
            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// Try to send pending data
        /// </summary>
        private void TrySend()
        {
            if (_sending)
                return;

            if (!IsConnected)
                return;

            try
            {
                // Async write with the write handler
                _sending = true;
                _sendEventArg.RemoteEndPoint = _sendEndpoint;
                _sendEventArg.SetBuffer(_sendBuffer.Data, 0, (int)(_sendBuffer.Size));
                if (!Socket.SendToAsync(_sendEventArg))
                    ProcessSendTo(_sendEventArg);
            }
            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// Clear send/receive buffers
        /// </summary>
        private void ClearBuffers()
        {
            // Clear send buffers
            _sendBuffer.Clear();

            // Update statistic
            BytesPending = 0;
            BytesSending = 0;
        }

        #endregion

        #region IO processing

        /// <summary>
        /// This method is called whenever a receive or send operation is completed on a socket
        /// </summary>
        private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            // Determine which type of operation just completed and call the associated handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.ReceiveFrom:
                    ProcessReceiveFrom(e);
                    break;
                case SocketAsyncOperation.SendTo:
                    ProcessSendTo(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }

        }

        /// <summary>
        /// This method is invoked when an asynchronous receive from operation completes
        /// </summary>
        private void ProcessReceiveFrom(SocketAsyncEventArgs e)
        {
            _receiving = false;

            if (!IsConnected)
                return;

            // Disconnect on error
            if (e.SocketError != SocketError.Success)
            {
                SendError(e.SocketError);
                Disconnect();
                return;
            }

            long size = e.BytesTransferred;

            // Received some data from the client
            if (size > 0)
            {
                // Update statistic
                DatagramsReceived++;
                BytesReceived += size;

                // If the receive buffer is full increase its size
                if (_receiveBuffer.Capacity == size)
                    _receiveBuffer.Reserve(2 * size);

                // Call the datagram received handler
                OnReceived(e.RemoteEndPoint as IPEndPoint, _receiveBuffer.Data, size);
            }
        }

        /// <summary>
        /// This method is invoked when an asynchronous send to operation completes
        /// </summary>
        private void ProcessSendTo(SocketAsyncEventArgs e)
        {
            _sending = false;

            if (!IsConnected)
                return;

            // Disconnect on error
            if (e.SocketError != SocketError.Success)
            {
                SendError(e.SocketError);
                Disconnect();
                return;
            }

            long sent = e.BytesTransferred;

            // Send some data to the client
            if (sent > 0)
            {
                // Update statistic
                BytesSending = 0;
                BytesSent += sent;

                // Clear the send buffer
                _sendBuffer.Clear();

                // Call the buffer sent handler
                OnSent(_sendEndpoint, sent);
            }
        }

        #endregion

        #region Session handlers

        /// <summary>
        /// Handle client connected notification
        /// </summary>
        protected virtual void OnConnected() { }
        /// <summary>
        /// Handle client disconnected notification
        /// </summary>
        protected virtual void OnDisconnected() { }

        /// <summary>
        /// Handle client joined multicast group notification
        /// </summary>
        /// <param name="address">IP address</param>
        protected virtual void OnJoinedMulticastGroup(IPAddress address) { }
        /// <summary>
        /// Handle client left multicast group notification
        /// </summary>
        /// <param name="address">IP address</param>
        protected virtual void OnLeftMulticastGroup(IPAddress address) { }

        /// <summary>
        /// Handle datagram received notification
        /// </summary>
        /// <param name="endpoint">Received endpoint</param>
        /// <param name="buffer">Received datagram buffer</param>
        /// <param name="size">Received datagram buffer size</param>
        /// <remarks>
        /// Notification is called when another datagram was received from some endpoint
        /// </remarks>
        protected virtual void OnReceived(IPEndPoint endpoint, byte[] buffer, long size) { }
        /// <summary>
        /// Handle datagram sent notification
        /// </summary>
        /// <param name="endpoint">Endpoint of sent datagram</param>
        /// <param name="sent">Size of sent datagram buffer</param>
        /// <remarks>
        /// Notification is called when a datagram was sent to the server.
        /// This handler could be used to send another datagram to the server for instance when the pending size is zero.
        /// </remarks>
        protected virtual void OnSent(IPEndPoint endpoint, long sent) { }

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError(SocketError error) { }

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
                (error == SocketError.OperationAborted))
                return;

            OnError(error);
        }

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
                    Disconnect();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                _disposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~UdpClient()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        #endregion
    }
}
