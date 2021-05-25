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

        /// <summary>
        /// WebSocket random nonce
        /// </summary>
        public byte[] WsNonce => WebSocket.WsNonce;

        #region WebSocket connection methods

        public override bool Connect() { _syncConnect = true; return base.Connect(); }
        public override bool ConnectAsync() { _syncConnect = false; return base.ConnectAsync(); }
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
            while (!WebSocket.WsFinalReceived)
            {
                while (!WebSocket.WsFrameReceived)
                {
                    int required = WebSocket.RequiredReceiveFrameSize();
                    cache.Resize(required);
                    int received = (int)base.Receive(cache.Data, 0, required);
                    if (received != required)
                        return result.ExtractString(0, result.Data.Length);
                    WebSocket.PrepareReceiveFrame(cache.Data, 0, received);
                }
                if (!WebSocket.WsFinalReceived)
                    WebSocket.PrepareReceiveFrame(null, 0, 0);
            }

            // Copy WebSocket frame data
            result.Append(WebSocket.WsReceiveFinalBuffer.ToArray(), 0, WebSocket.WsReceiveFinalBuffer.Count);
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
            while (!WebSocket.WsFinalReceived)
            {
                while (!WebSocket.WsFrameReceived)
                {
                    int required = WebSocket.RequiredReceiveFrameSize();
                    cache.Resize(required);
                    int received = (int)base.Receive(cache.Data, 0, required);
                    if (received != required)
                        return result;
                    WebSocket.PrepareReceiveFrame(cache.Data, 0, received);
                }
                if (!WebSocket.WsFinalReceived)
                    WebSocket.PrepareReceiveFrame(null, 0, 0);
            }

            // Copy WebSocket frame data
            result.Append(WebSocket.WsReceiveFinalBuffer.ToArray(), 0, WebSocket.WsReceiveFinalBuffer.Count);
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

            // Send the WebSocket upgrade HTTP request
            if (_syncConnect)
                SendRequest(Request);
            else
                SendRequestAsync(Request);
        }

        protected override void OnDisconnecting()
        {
            if (WebSocket.WsHandshaked)
                OnWsDisconnecting();
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

            // Initialize new WebSocket random nonce
            WebSocket.InitWsNonce();
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
                // Prepare receive frame from the remaining response body
                var body = Response.Body;
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

        public virtual void OnWsConnecting(HttpRequest request) {}
        public virtual void OnWsConnected(HttpResponse response) {}
        public virtual bool OnWsConnecting(HttpRequest request, HttpResponse response) { return true; }
        public virtual void OnWsConnected(HttpRequest request) {}
        public virtual void OnWsDisconnecting() {}
        public virtual void OnWsDisconnected() {}
        public virtual void OnWsReceived(byte[] buffer, long offset, long size) {}
        public virtual void OnWsClose(byte[] buffer, long offset, long size) { CloseAsync(1000); }
        public virtual void OnWsPing(byte[] buffer, long offset, long size) { SendPongAsync(buffer, offset, size); }
        public virtual void OnWsPong(byte[] buffer, long offset, long size) {}
        public virtual void OnWsError(string error) { OnError(SocketError.SocketError); }
        public virtual void OnWsError(SocketError error) { OnError(error); }

        #endregion

        // Sync connect flag
        private bool _syncConnect;
    }
}
