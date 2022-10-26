using System;
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
        /// Initialize WebSocket client with a given DNS endpoint
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="endpoint">DNS endpoint</param>
        public WssClient(SslContext context, DnsEndPoint endpoint) : base(context, endpoint) { WebSocket = new WebSocket(this); }
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
        public virtual bool Close(int status) { SendClose(status, Span<byte>.Empty); base.Disconnect(); return true; }
        public virtual bool CloseAsync(int status) { SendCloseAsync(status, Span<byte>.Empty); base.DisconnectAsync(); return true; }

        #endregion

        #region WebSocket send text methods

        public long SendText(string text) => SendText(Encoding.UTF8.GetBytes(text));
        public long SendText(ReadOnlySpan<char> text) => SendText(Encoding.UTF8.GetBytes(text.ToArray()));
        public long SendText(byte[] buffer) => SendText(buffer.AsSpan());
        public long SendText(byte[] buffer, long offset, long size) => SendText(buffer.AsSpan((int)offset, (int)size));
        public long SendText(ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, buffer);
                return base.Send(WebSocket.WsSendBuffer.AsSpan());
            }
        }

        public bool SendTextAsync(string text) => SendTextAsync(Encoding.UTF8.GetBytes(text));
        public bool SendTextAsync(ReadOnlySpan<char> text) => SendTextAsync(Encoding.UTF8.GetBytes(text.ToArray()));
        public bool SendTextAsync(byte[] buffer) => SendTextAsync(buffer.AsSpan());
        public bool SendTextAsync(byte[] buffer, long offset, long size) => SendTextAsync(buffer.AsSpan((int)offset, (int)size));
        public bool SendTextAsync(ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, buffer);
                return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
            }
        }


        #endregion

        #region WebSocket send binary methods

        public long SendBinary(string text) => SendBinary(Encoding.UTF8.GetBytes(text));
        public long SendBinary(ReadOnlySpan<char> text) => SendBinary(Encoding.UTF8.GetBytes(text.ToArray()));
        public long SendBinary(byte[] buffer) => SendBinary(buffer.AsSpan());
        public long SendBinary(byte[] buffer, long offset, long size) => SendBinary(buffer.AsSpan((int)offset, (int)size));
        public long SendBinary(ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, buffer);
                return base.Send(WebSocket.WsSendBuffer.AsSpan());
            }
        }

        public bool SendBinaryAsync(string text) => SendBinaryAsync(Encoding.UTF8.GetBytes(text));
        public bool SendBinaryAsync(ReadOnlySpan<char> text) => SendBinaryAsync(Encoding.UTF8.GetBytes(text.ToArray()));
        public bool SendBinaryAsync(byte[] buffer) => SendBinaryAsync(buffer.AsSpan());
        public bool SendBinaryAsync(byte[] buffer, long offset, long size) => SendBinaryAsync(buffer.AsSpan((int)offset, (int)size));
        public bool SendBinaryAsync(ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, buffer);
                return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
            }
        }


        #endregion

        #region WebSocket send close methods

        public long SendClose(int status, string text) => SendClose(status, Encoding.UTF8.GetBytes(text));
        public long SendClose(int status, ReadOnlySpan<char> text) => SendClose(status, Encoding.UTF8.GetBytes(text.ToArray()));
        public long SendClose(int status, byte[] buffer) => SendClose(status, buffer.AsSpan());
        public long SendClose(int status, byte[] buffer, long offset, long size) => SendClose(status, buffer.AsSpan((int)offset, (int)size));
        public long SendClose(int status, ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, buffer, status);
                return base.Send(WebSocket.WsSendBuffer.AsSpan());
            }
        }

        public bool SendCloseAsync(int status, string text) => SendCloseAsync(status, Encoding.UTF8.GetBytes(text));
        public bool SendCloseAsync(int status, ReadOnlySpan<char> text) => SendCloseAsync(status, Encoding.UTF8.GetBytes(text.ToArray()));
        public bool SendCloseAsync(int status, byte[] buffer) => SendCloseAsync(status, buffer.AsSpan());
        public bool SendCloseAsync(int status, byte[] buffer, long offset, long size) => SendCloseAsync(status, buffer.AsSpan((int)offset, (int)size));
        public bool SendCloseAsync(int status, ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, true, buffer, status);
                return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
            }
        }


        #endregion

        #region WebSocket send ping methods

        public long SendPing(string text) => SendPing(Encoding.UTF8.GetBytes(text));
        public long SendPing(ReadOnlySpan<char> text) => SendPing(Encoding.UTF8.GetBytes(text.ToArray()));
        public long SendPing(byte[] buffer) => SendPing(buffer.AsSpan());
        public long SendPing(byte[] buffer, long offset, long size) => SendPing(buffer.AsSpan((int)offset, (int)size));
        public long SendPing(ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, buffer);
                return base.Send(WebSocket.WsSendBuffer.AsSpan());
            }
        }

        public bool SendPingAsync(string text) => SendPingAsync(Encoding.UTF8.GetBytes(text));
        public bool SendPingAsync(ReadOnlySpan<char> text) => SendPingAsync(Encoding.UTF8.GetBytes(text.ToArray()));
        public bool SendPingAsync(byte[] buffer) => SendPingAsync(buffer.AsSpan());
        public bool SendPingAsync(byte[] buffer, long offset, long size) => SendPingAsync(buffer.AsSpan((int)offset, (int)size));
        public bool SendPingAsync(ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, buffer);
                return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
            }
        }


        #endregion

        #region WebSocket send pong methods

        public long SendPong(string text) => SendPong(Encoding.UTF8.GetBytes(text));
        public long SendPong(ReadOnlySpan<char> text) => SendPong(Encoding.UTF8.GetBytes(text.ToArray()));
        public long SendPong(byte[] buffer) => SendPong(buffer.AsSpan());
        public long SendPong(byte[] buffer, long offset, long size) => SendPong(buffer.AsSpan((int)offset, (int)size));
        public long SendPong(ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, buffer);
                return base.Send(WebSocket.WsSendBuffer.AsSpan());
            }
        }

        public bool SendPongAsync(string text) => SendPongAsync(Encoding.UTF8.GetBytes(text));
        public bool SendPongAsync(ReadOnlySpan<char> text) => SendPongAsync(Encoding.UTF8.GetBytes(text.ToArray()));
        public bool SendPongAsync(byte[] buffer) => SendPongAsync(buffer.AsSpan());
        public bool SendPongAsync(byte[] buffer, long offset, long size) => SendPongAsync(buffer.AsSpan((int)offset, (int)size));
        public bool SendPongAsync(ReadOnlySpan<byte> buffer)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, buffer);
                return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
            }
        }

        #endregion

        #region WebSocket receive methods

        public string ReceiveText()
        {
            var result = new Buffer();

            if (!WebSocket.WsHandshaked)
                return result.ExtractString(0, result.Data.Length);

            var cache = new Buffer();

            // Receive WebSocket frame data
            while (!WebSocket.WsFinalReceived)
            {
                while (!WebSocket.WsFrameReceived)
                {
                    long required = WebSocket.RequiredReceiveFrameSize();
                    cache.Resize(required);
                    long received = (int)base.Receive(cache.Data, 0, required);
                    if (received != required)
                        return result.ExtractString(0, result.Data.Length);
                    WebSocket.PrepareReceiveFrame(cache.Data, 0, received);
                }
                if (!WebSocket.WsFinalReceived)
                    WebSocket.PrepareReceiveFrame(null, 0, 0);
            }

            // Copy WebSocket frame data
            result.Append(WebSocket.WsReceiveFinalBuffer);
            WebSocket.PrepareReceiveFrame(null, 0, 0);
            return result.ExtractString(0, result.Data.Length);
        }

        public Buffer ReceiveBinary()
        {
            var result = new Buffer();

            if (!WebSocket.WsHandshaked)
                return result;

            var cache = new Buffer();

            // Receive WebSocket frame data
            while (!WebSocket.WsFinalReceived)
            {
                while (!WebSocket.WsFrameReceived)
                {
                    long required = WebSocket.RequiredReceiveFrameSize();
                    cache.Resize(required);
                    long received = (int)base.Receive(cache.Data, 0, required);
                    if (received != required)
                        return result;
                    WebSocket.PrepareReceiveFrame(cache.Data, 0, received);
                }
                if (!WebSocket.WsFinalReceived)
                    WebSocket.PrepareReceiveFrame(null, 0, 0);
            }

            // Copy WebSocket frame data
            result.Append(WebSocket.WsReceiveFinalBuffer);
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
        public virtual void OnWsClose(byte[] buffer, long offset, long size, int status = 1000) { CloseAsync(status); }
        public virtual void OnWsPing(byte[] buffer, long offset, long size) { SendPongAsync(buffer, offset, size); }
        public virtual void OnWsPong(byte[] buffer, long offset, long size) {}
        public virtual void OnWsError(string error) { OnError(SocketError.SocketError); }
        public virtual void OnWsError(SocketError error) { OnError(error); }

        #endregion

        // Sync connect flag
        private bool _syncConnect;
    }
}
