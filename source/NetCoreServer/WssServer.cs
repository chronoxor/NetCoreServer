using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetCoreServer
{
    /// <summary>
    /// WebSocket secure server
    /// </summary>
    /// <remarks> WebSocket secure server is used to communicate with clients using WebSocket protocol. Thread-safe.</remarks>
    public class WssServer : HttpsServer, IWebSocket
    {
        internal readonly WebSocket WebSocket;

        /// <summary>
        /// Initialize WebSocket server with a given IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public WssServer(SslContext context, IPAddress address, int port) : base(context, address, port) { WebSocket = new WebSocket(this); }
        /// <summary>
        /// Initialize WebSocket server with a given IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public WssServer(SslContext context, string address, int port) : base(context, address, port) { WebSocket = new WebSocket(this); }
        /// <summary>
        /// Initialize WebSocket server with a given IP endpoint
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="endpoint">IP endpoint</param>
        public WssServer(SslContext context, IPEndPoint endpoint) : base(context, endpoint) { WebSocket = new WebSocket(this); }

        public virtual bool CloseAll(int status)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, false, null, 0, 0, status);
                if (!Multicast(WebSocket.WsSendBuffer.ToArray()))
                    return false;

                return base.DisconnectAll();
            }
        }

        public override bool Multicast(byte[] buffer, long offset, long size)
        {
            if (!IsStarted)
                return false;

            if (size == 0)
                return true;

            // Multicast data to all WebSocket sessions
            foreach (var session in Sessions.Values)
            {
                if (session is WssSession wssSession)
                {
                    if (wssSession.WebSocket.WsHandshaked)
                        wssSession.SendAsync(buffer, offset, size);
                }
            }

            return true;
        }

        #region WebSocket multicast text methods

        public bool MulticastText(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, false, buffer, offset, size);
                return Multicast(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool MulticastText(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, false, data, 0, data.Length);
                return Multicast(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket multicast binary methods

        public bool MulticastBinary(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, false, buffer, offset, size);
                return Multicast(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool MulticastBinary(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, false, data, 0, data.Length);
                return Multicast(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket multicast ping methods

        public bool SendPing(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, false, buffer, offset, size);
                return Multicast(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendPing(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, false, data, 0, data.Length);
                return Multicast(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket multicast pong methods

        public bool SendPong(byte[] buffer, long offset, long size)
        {
            lock (WebSocket.WsSendLock)
            {
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, false, buffer, offset, size);
                return Multicast(WebSocket.WsSendBuffer.ToArray());
            }
        }

        public bool SendPong(string text)
        {
            lock (WebSocket.WsSendLock)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, false, data, 0, data.Length);
                return Multicast(WebSocket.WsSendBuffer.ToArray());
            }
        }

        #endregion

        #region IWebSocket implementation

        public void OnWsConnecting(HttpRequest request) { WebSocket.OnWsConnecting(request); }
        public void OnWsConnected(HttpResponse response) { WebSocket.OnWsConnected(response); }
        public bool OnWsConnecting(HttpRequest request, HttpResponse response) { return WebSocket.OnWsConnecting(request, response); }
        public void OnWsConnected(HttpRequest request) { WebSocket.OnWsConnected(request); }
        public void OnWsDisconnected() { WebSocket.OnWsDisconnected(); }
        public void OnWsReceived(byte[] buffer, long offset, long size) { WebSocket.OnWsReceived(buffer, offset, size); }
        public void OnWsClose(byte[] buffer, long offset, long size) { WebSocket.OnWsClose(buffer, offset, size); }
        public void OnWsPing(byte[] buffer, long offset, long size) { WebSocket.OnWsPing(buffer, offset, size); }
        public void OnWsPong(byte[] buffer, long offset, long size) { WebSocket.OnWsPong(buffer, offset, size); }
        public void OnWsError(string error) { WebSocket.OnWsError(error); }
        public void OnWsError(SocketError error) { WebSocket.OnWsError(error); }
        public void SendResponse(HttpResponse response) { WebSocket.SendResponse(response); }

        #endregion

        protected override SslSession CreateSession() { return new WssSession(this); }
    }
}
