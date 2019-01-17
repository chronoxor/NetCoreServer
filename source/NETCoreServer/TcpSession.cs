using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace NetCoreServer
{
    /// <summary>
    /// TCP session is used to read and write data from the connected TCP client
    /// </summary>
    /// <remarks>Thread-safe</remarks>
    public class TcpSession
    {
        /// <summary>
        /// Initialize the session with a given server
        /// </summary>
        /// <param name="server">TCP server</param>
        public TcpSession(TcpServer server)
        {
            Id = Guid.NewGuid();
            Server = server;
        }

        /// <summary>
        /// TCP session Id
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Server
        /// </summary>
        public TcpServer Server { get; }
        /// <summary>
        /// Socket
        /// </summary>
        public Socket Socket { get; private set; }

        /// <summary>
        /// Number of bytes pending sent by the session
        /// </summary>
        public long BytesPending { get; private set; }
        /// <summary>
        /// Number of bytes sending by the session
        /// </summary>
        public long BytesSending { get; private set; }
        /// <summary>
        /// Number of bytes sent by the session
        /// </summary>
        public long BytesSent { get; private set; }
        /// <summary>
        /// Number of bytes received by the session
        /// </summary>
        public long BytesReceived { get; private set; }

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

        #region Connect/Disconnect session

        /// <summary>
        /// Is the session connected?
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Connect the session
        /// </summary>
        /// <param name="socket">Session socket</param>
        public void Connect(Socket socket)
        {
            Socket = socket;

            // Apply the option: keep alive
            if (Server.OptionKeepAlive)
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            // Apply the option: no delay
            if (Server.OptionNoDelay)
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);

            // Prepare receive & send buffers
            _receiveBuffer.Capacity = OptionReceiveBufferSize;
            _sendBufferMain.Capacity = OptionSendBufferSize;
            _sendBufferFlush.Capacity = OptionSendBufferSize;

            // Reset statistic
            BytesPending = 0;
            BytesSending = 0;
            BytesSent = 0;
            BytesReceived = 0;

            // Update the connected flag
            IsConnected = true;

            // Call the session connected handler
            OnConnected();

            // Call the session connected handler in the server
            Server.OnConnectedInternal(this);

            // Call the empty send buffer handler
            if (_sendBufferMain.Count == 0)
                OnEmpty();

            // Try to receive something from the client
            TryReceive();
        }

        /// <summary>
        /// Disconnect the session
        /// </summary>
        /// <returns>'true' if the section was successfully disconnected, 'false' if the section is already disconnected</returns>
        public bool Disconnect()
        {
            if (!IsConnected)
                return false;

            // Close the session socket
            Socket.Close();

            // Update the connected flag
            IsConnected = false;

            // Clear send/receive buffers
            ClearBuffers();

            // Call the session disconnected handler
            OnDisconnected();

            // Call the session disconnected handler in the server
            Server.OnDisconnectedInternal(this);

            // Unregister session
            Server.UnregisterSession(Id);

            return true;
        }

        #endregion

        #region Send/Recieve data

        // Receive buffer & cache
        private readonly List<byte> _receiveBuffer = new List<byte>();
        // Send buffer & cache
        private readonly object _sendLock = new object();
        private readonly List<byte> _sendBufferMain = new List<byte>();
        private readonly List<byte> _sendBufferFlush = new List<byte>();
        private long _sendBufferFlushOffset;

        /// <summary>
        /// Send data into the session
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
        public bool Send(byte[] buffer) { return Send(buffer, 0, buffer.Length); }

        /// <summary>
        /// Send data into the session
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
        public bool Send(byte[] buffer, long offset, long size)
        {
            return true;
        }

        /// <summary>
        /// Send text into the session
        /// </summary>
        /// <param name="text">Text string to send</param>
        /// <returns>'true' if the text was successfully sent, 'false' if the session is not connected</returns>
        public bool Send(string text)
        {
            return true;
        }

        /// <summary>
        /// Try to receive new data
        /// </summary>
        private void TryReceive() {}
        /// <summary>
        /// Try to send pending data
        /// </summary>
        private void TrySend() {}

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

        #region Session handlers

        /// <summary>
        /// Handle client connected notification
        /// </summary>
        protected virtual void OnConnected() {}
        /// <summary>
        /// Handle client disconnected notification
        /// </summary>
        protected virtual void OnDisconnected() {}

        /// <summary>
        /// Handle buffer received notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <remarks>
        /// Notification is called when another chunk of buffer was received from the client
        /// </remarks>
        protected virtual void OnReceived(byte[] buffer) {}
        /// <summary>
        /// Handle buffer sending notification
        /// </summary>
        /// <param name="size">Size of send buffer</param>
        /// <returns>Allow send flag</returns>
        protected virtual bool OnSending(long size) { return true; }
        /// <summary>
        /// Handle buffer sent notification
        /// </summary>
        /// <param name="sent">Size of sent buffer</param>
        /// <param name="pending">Size of pending buffer</param>
        /// <remarks>
        /// Notification is called when another chunk of buffer was sent to the client.
        /// This handler could be used to send another buffer to the client for instance when the pending size is zero.
        /// </remarks>
        protected virtual void OnSent(long sent, long pending) {}

        /// <summary>
        /// Handle empty send buffer notification
        /// </summary>
        /// <remarks>
        /// Notification is called when the send buffer is empty and ready for a new data to send.
        /// This handler could be used to send another buffer to the client.
        /// </remarks>
        protected virtual void OnEmpty() {}

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError(SocketError error) {}

        #endregion
    }
}
