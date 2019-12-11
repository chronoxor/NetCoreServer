using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetCoreServer
{
    /// <summary>
    /// WebSocket client
    /// </summary>
    /// <remarks>WebSocket client is used to communicate with WebSocket server. Thread-safe.</remarks>
    public class WsClient : HttpClient, IWebSocket
    {
        protected WebSocket webSocket;

        /// <summary>
        /// Initialize WebSocket client with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public WsClient(IPAddress address, int port) : base(address, port) { webSocket = new WebSocket(this); }
        /// <summary>
        /// Initialize WebSocket client with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public WsClient(string address, int port) : base(address, port) { webSocket = new WebSocket(this); }
        /// <summary>
        /// Initialize WebSocket client with a given IP endpoint
        /// </summary>
        /// <param name="endpoint">IP endpoint</param>
        public WsClient(IPEndPoint endpoint) : base(endpoint) { webSocket = new WebSocket(this); }

        #region WebSocket connection methods

        public override bool Connect() { _syncConnect = true; return base.Connect(); }
        public override bool ConnectAsync() { _syncConnect = true; return base.ConnectAsync(); }
        public virtual bool Close(int status) { SendClose(status, null, 0, 0); base.Disconnect(); return true; }
        public virtual bool CloseAsync(int status) { SendCloseAsync(status, null, 0, 0); base.DisconnectAsync(); return true; }

        #endregion

        #region WebSocket send text methods

        public long SendText(byte[] buffer, long offset, long size) 
        { 
            lock(webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, buffer, offset, size);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }      
        }

        public long SendText(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendTextAsync(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, buffer, offset, size);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendTextAsync(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket send binary methods

        public long SendBinary(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, buffer, offset, size);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public long SendBinary(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendBinaryAsync(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, buffer, offset, size);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendBinaryAsync(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket send close methods

        public long SendClose(int status, byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, buffer, offset, size, status);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public long SendClose(int status, string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, Encoding.UTF8.GetBytes(text), 0, text.Length, status);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendCloseAsync(int status, byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, buffer, offset, size, status);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendCloseAsync(int status, string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, Encoding.UTF8.GetBytes(text), 0, text.Length, status);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket send ping methods

        public long SendPing(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, buffer, offset, size);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public long SendPing(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendPingAsync(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, buffer, offset, size);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendPingAsync(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket send pong methods

        public long SendPong(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, buffer, offset, size);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public long SendPong(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.Send(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendPongAsync(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, buffer, offset, size);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendPongAsync(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.SendAsync(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket receive methods

        public string ReceiveText()
        {
            Buffer result = new Buffer();

            if (!webSocket.wsHandshaked)
                return result.ExtractString(0, result.Data.Length);

            Buffer cache = new Buffer();

            // Receive WebSocket frame data
            while (!webSocket.wsReceived)
            {
                int required = webSocket.RequiredReceiveFrameSize();
                cache.Resize(required);
                int received = (int) base.Receive(cache.Data, 0, required);
                if (received != required)
                    return result.ExtractString(0, result.Data.Length);
                webSocket.PrepareReceiveFrame(cache.Data, 0, received);
            }

            // Copy WebSocket frame data
            result.Append(webSocket.wsReceiveBuffer.ToArray(), webSocket.wsHeaderSize, webSocket.wsHeaderSize + webSocket.wsPayloadSize);
            webSocket.PrepareReceiveFrame(null, 0, 0);
            return result.ExtractString(0, result.Data.Length);
        }

        public Buffer ReceiveBinary()
        {
            Buffer result = new Buffer();

            if (!webSocket.wsHandshaked)
                return result;

            Buffer cache = new Buffer();

            // Receive WebSocket frame data
            while (!webSocket.wsReceived)
            {
                int required = webSocket.RequiredReceiveFrameSize();
                cache.Resize(required);
                int received = (int)base.Receive(cache.Data, 0, required);
                if (received != required)
                    return result;
                webSocket.PrepareReceiveFrame(cache.Data, 0, received);
            }

            // Copy WebSocket frame data
            result.Append(webSocket.wsReceiveBuffer.ToArray(), webSocket.wsHeaderSize, webSocket.wsHeaderSize + webSocket.wsPayloadSize);
            webSocket.PrepareReceiveFrame(null, 0, 0);
            return result;
        }

        #endregion

        #region Session handlers

        protected override void OnConnected()
        {
            // Clear WebSocket send/receive buffers
            webSocket.ClearWSBuffers();

            // Fill the WebSocket upgrade HTTP request
            OnWSConnecting(Request);

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
            if (webSocket.wsHandshaked)
            {
                webSocket.wsHandshaked = false;
                OnWSDisconnected();
            }

            // Reset WebSocket upgrade HTTP request and response
            Request.Clear();
            Response.Clear();

            // Clear WebSocket send/receive buffers
            webSocket.ClearWSBuffers();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Check for WebSocket handshaked status
            if (webSocket.wsHandshaked)
            {
                // Prepare receive frame
                webSocket.PrepareReceiveFrame(buffer, offset, size);
                return;
            }

            base.OnReceived(buffer, offset, size);
        }

        protected override void OnReceivedResponseHeader(HttpResponse response)
        {
            // Check for WebSocket handshaked status
            if (webSocket.wsHandshaked)
                return;

            // Try to perform WebSocket upgrade
            if (!webSocket.PerformClientUpgrade(response, Id))
            {
                base.OnReceivedResponseHeader(response);
                return;
            }
        }

        protected override void OnReceivedResponse(HttpResponse response)
        {
            // Check for WebSocket handshaked status
            if (webSocket.wsHandshaked)
            {
                // Prepare receive frame from the remaining request body
                var body = Request.Body;
                webSocket.PrepareReceiveFrame(Encoding.UTF8.GetBytes(body), 0, body.Length);
                return;
            }

            base.OnReceivedResponse(response);
        }

        protected override void OnReceivedResponseError(HttpResponse response, string error)
        {
            // Check for WebSocket handshaked status
            if (webSocket.wsHandshaked)
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
        public virtual void OnWSConnecting(HttpRequest request) { }
        /// <summary>
        /// Handle WebSocket client connected notification
        /// </summary>
        /// <param name="response">WebSocket upgrade HTTP response</param>
        public virtual void OnWSConnected(HttpResponse response) { }
        /// <summary>
        /// Handle WebSocket server session validating notification
        /// </summary>
        /// <remarks>Notification is called when WebSocket client is connecting to the server.You can handle the connection and validate WebSocket upgrade HTTP request.</remarks>
        /// <param name="request">WebSocket upgrade HTTP request</param>
        /// <param name="response">WebSocket upgrade HTTP response</param>
        /// <returns>return 'true' if the WebSocket update request is valid, 'false' if the WebSocket update request is not valid</returns>
        public virtual bool OnWSConnecting(HttpRequest request, HttpResponse response) { return true; }
        /// <summary>
        /// Handle WebSocket server session connected notification
        /// </summary>
        /// <param name="request">WebSocket upgrade HTTP request</param>
        public virtual void OnWSConnected(HttpRequest request) { }
        /// <summary>
        /// Handle WebSocket client disconnected notification
        /// </summary>
        public virtual void OnWSDisconnected() { }
        /// <summary>
        /// Handle WebSocket received notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        public virtual void OnWSReceived(byte[] buffer, long offset, long size) { }
        /// <summary>
        /// Handle WebSocket client close notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        public virtual void OnWSClose(byte[] buffer, long offset, long size) { CloseAsync(1000); }
        /// <summary>
        /// Handle WebSocket ping notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        public virtual void OnWSPing(byte[] buffer, long offset, long size) { SendPongAsync(buffer, offset, size); }
        /// <summary>
        /// Handle WebSocket pong notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        public virtual void OnWSPong(byte[] buffer, long offset, long size) { }
        /// <summary>
        /// Handle WebSocket error notification
        /// </summary>
        /// <param name="message">Error message</param>
        public virtual void OnWSError(SocketError error) { OnError(error); }
        /// <summary>
        /// Send WebSocket server upgrade response
        /// </summary>
        /// <param name="response">WebSocket upgrade HTTP response</param>
        public virtual void SendResponse(HttpResponse response) { }

        #endregion

        // Sync connect flag
        private bool _syncConnect;
    }
}
