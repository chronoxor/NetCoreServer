using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetCoreServer
{
    /// <summary>
    /// WebSocket secure client
    /// </summary>
    /// <remarks>WebSocket secure client is used to communicate with secure WebSocket server. Thread-safe.</remarks>
    public class WssClient : HttpsClient, IWebSocket
    {
        internal readonly WebSocket WebSocket;

        /// <summary>
        /// Initialize WebSocket client with a given IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public WssClient(SslContext context, IPAddress address, int port) : base(context, address, port) { WebSocket = new WebSocket(this); }
        /// <summary>
        /// Initialize WebSocket client with a given IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public WssClient(SslContext context, string address, int port) : base(context, address, port) { WebSocket = new WebSocket(this); }
        /// <summary>
        /// Initialize WebSocket client with a given IP endpoint
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="endpoint">IP endpoint</param>
        public WssClient(SslContext context, IPEndPoint endpoint) : base(context, endpoint) { WebSocket = new WebSocket(this); }

        #region WebSocket connection methods

        public override bool Connect() { _syncConnect = true; return base.Connect(); }
        public override bool ConnectAsync() { _syncConnect = true; return base.ConnectAsync(); }
        public virtual bool Close(int status) { SendClose(status, null, 0, 0); base.Disconnect(); return true; }
        public virtual bool CloseAsync(int status) { SendCloseAsync(status, null, 0, 0); base.DisconnectAsync(); return true; }

        #endregion

        #region WebSocket send text methods

        public long SendText(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, buffer, offset, size);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public long SendText(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, data, 0, data.Length);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendTextAsync(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, buffer, offset, size);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendTextAsync(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, data, 0, data.Length);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket send binary methods

        public long SendBinary(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, buffer, offset, size);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public long SendBinary(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, data, 0, data.Length);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendBinaryAsync(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, buffer, offset, size);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendBinaryAsync(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, data, 0, data.Length);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket send close methods

        public long SendClose(int status, byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, buffer, offset, size, status);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public long SendClose(int status, string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, data, 0, data.Length, status);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendCloseAsync(int status, byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, buffer, offset, size, status);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendCloseAsync(int status, string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, data, 0, data.Length, status);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket send ping methods

        public long SendPing(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, buffer, offset, size);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public long SendPing(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, data, 0, data.Length);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendPingAsync(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, buffer, offset, size);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendPingAsync(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, data, 0, data.Length);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket send pong methods

        public long SendPong(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, buffer, offset, size);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public long SendPong(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, data, 0, data.Length);
                return base.Send(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendPongAsync(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, buffer, offset, size);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendPongAsync(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, data, 0, data.Length);
                return base.SendAsync(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket receive methods

        public string ReceiveText()
        {
            Buffer result = new Buffer();

            if (!WebSocket.WsHandshaked)
                return result.ExtractString(0, result.Data.Length);

            Buffer cache = new Buffer();

            // Receive WebSocket frame data
            while (!WebSocket.WsReceived)
            {
                int required = WebSocket.RequiredReceiveFrameSize();
                cache.Resize(required);
                int received = (int)base.Receive(cache.Data, 0, required);
                if (received != required)
                    return result.ExtractString(0, result.Data.Length);
                WebSocket.PrepareReceiveFrame(cache.Data, 0, received);
            }

            // Copy WebSocket frame data
            result.Append(WebSocket.WsReceiveBuffer.ToArray(), WebSocket.WsHeaderSize, WebSocket.WsHeaderSize + WebSocket.WsPayloadSize);
            WebSocket.PrepareReceiveFrame(null, 0, 0);
            return result.ExtractString(0, result.Data.Length);
        }

        public Buffer ReceiveBinary()
        {
            Buffer result = new Buffer();

            if (!WebSocket.WsHandshaked)
                return result;

            Buffer cache = new Buffer();

            // Receive WebSocket frame data
            while (!WebSocket.WsReceived)
            {
                int required = WebSocket.RequiredReceiveFrameSize();
                cache.Resize(required);
                int received = (int)base.Receive(cache.Data, 0, required);
                if (received != required)
                    return result;
                WebSocket.PrepareReceiveFrame(cache.Data, 0, received);
            }

            // Copy WebSocket frame data
            result.Append(WebSocket.WsReceiveBuffer.ToArray(), WebSocket.WsHeaderSize, WebSocket.WsHeaderSize + WebSocket.WsPayloadSize);
            WebSocket.PrepareReceiveFrame(null, 0, 0);
            return result;
        }

        #endregion

        #region Session handlers

        protected override void OnHandshaked()
        {
            // Clear WebSocket send/receive buffers
            WebSocket.ClearWsBuffers();

            // Fill the WebSocket upgrade HTTP request
            OnWsConnecting(Request);

            // Set empty body of the WebSocket upgrade HTTP request
            Request.SetBody();

            // Send the WebSocket upgrade HTTP request
            if (_syncConnect)
                Send(Request.Cache.Data);
            else
                SendAsync(Request.Cache.Data);
        }

        protected override void OnDisconnected()
        {
            // Disconnect WebSocket
            if (WebSocket.WsHandshaked)
            {
                WebSocket.WsHandshaked = false;
                OnWsDisconnected();
            }

            // Reset WebSocket upgrade HTTP request and response
            Request.Clear();
            Response.Clear();

            // Clear WebSocket send/receive buffers
            WebSocket.ClearWsBuffers();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Check for WebSocket handshaked status
            if (WebSocket.WsHandshaked)
            {
                // Prepare receive frame
                WebSocket.PrepareReceiveFrame(buffer, offset, size);
                return;
            }

            base.OnReceived(buffer, offset, size);
        }

        protected override void OnReceivedResponseHeader(HttpResponse response)
        {
            // Check for WebSocket handshaked status
            if (WebSocket.WsHandshaked)
                return;

            // Try to perform WebSocket upgrade
            if (!WebSocket.PerformClientUpgrade(response, Id))
            {
                base.OnReceivedResponseHeader(response);
                return;
            }
        }

        protected override void OnReceivedResponse(HttpResponse response)
        {
            // Check for WebSocket handshaked status
            if (WebSocket.WsHandshaked)
            {
                // Prepare receive frame from the remaining request body
                var body = Request.Body;
                var data = Encoding.UTF8.GetBytes(body);
                WebSocket.PrepareReceiveFrame(data, 0, data.Length);
                return;
            }

            base.OnReceivedResponse(response);
        }

        protected override void OnReceivedResponseError(HttpResponse response, string error)
        {
            // Check for WebSocket handshaked status
            if (WebSocket.WsHandshaked)
            {
                OnError(new SocketError());
                return;
            }

            base.OnReceivedResponseError(response, error);
        }

        #endregion

        #region Web socket handlers

        /// <summary>
        /// Handle WebSocket client connecting notification
        /// </summary>
        /// <remarks>Notification is called when WebSocket client is connecting to the server.You can handle the connection and change WebSocket upgrade HTTP request by providing your own headers.</remarks>
        /// <param name="request">WebSocket upgrade HTTP request</param>
        public virtual void OnWsConnecting(HttpRequest request) {}

        /// <summary>
        /// Handle WebSocket client connected notification
        /// </summary>
        /// <param name="response">WebSocket upgrade HTTP response</param>
        public virtual void OnWsConnected(HttpResponse response) {}

        /// <summary>
        /// Handle WebSocket server session validating notification
        /// </summary>
        /// <remarks>Notification is called when WebSocket client is connecting to the server.You can handle the connection and validate WebSocket upgrade HTTP request.</remarks>
        /// <param name="request">WebSocket upgrade HTTP request</param>
        /// <param name="response">WebSocket upgrade HTTP response</param>
        /// <returns>return 'true' if the WebSocket update request is valid, 'false' if the WebSocket update request is not valid</returns>
        public virtual bool OnWsConnecting(HttpRequest request, HttpResponse response) { return true; }

        /// <summary>
        /// Handle WebSocket server session connected notification
        /// </summary>
        /// <param name="request">WebSocket upgrade HTTP request</param>
        public virtual void OnWsConnected(HttpRequest request) {}

        /// <summary>
        /// Handle WebSocket client disconnected notification
        /// </summary>
        public virtual void OnWsDisconnected() {}

        /// <summary>
        /// Handle WebSocket received notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        public virtual void OnWsReceived(byte[] buffer, long offset, long size) {}

        /// <summary>
        /// Handle WebSocket client close notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        public virtual void OnWsClose(byte[] buffer, long offset, long size) { CloseAsync(1000); }

        /// <summary>
        /// Handle WebSocket ping notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        public virtual void OnWsPing(byte[] buffer, long offset, long size) { SendPongAsync(buffer, offset, size); }

        /// <summary>
        /// Handle WebSocket pong notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        public virtual void OnWsPong(byte[] buffer, long offset, long size) {}

        /// <summary>
        /// Handle WebSocket error notification
        /// </summary>
        /// <param name="error">Error message</param>
        public virtual void OnWsError(string error) { OnError(SocketError.SocketError); }

        /// <summary>
        /// Handle socket error notification
        /// </summary>
        /// <param name="error">Socket error</param>
        public virtual void OnWsError(SocketError error) { OnError(error); }

        /// <summary>
        /// Send WebSocket server upgrade response
        /// </summary>
        /// <param name="response">WebSocket upgrade HTTP response</param>
        public virtual void SendResponse(HttpResponse response) {}

        #endregion

        // Sync connect flag
        private bool _syncConnect;
    }
}