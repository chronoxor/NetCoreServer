using System.Net;
using System.Text;

namespace NetCoreServer
{
    /// <summary>
    /// WebSocket server
    /// </summary>
    /// <remarks> WebSocket server is used to communicate with clients using WebSocket protocol. Thread-safe.</remarks>
    public class WsServer : HttpServer, IWebSocket
    {
        protected WebSocket webSocket;

        /// <summary>
        /// Initialize WebSocket server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public WsServer(IPAddress address, int port) : base(address, port) { webSocket = new WebSocket(this); }
        /// <summary>
        /// Initialize WebSocket server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public WsServer(string address, int port) : base(address, port) { webSocket = new WebSocket(this); }
        /// <summary>
        /// Initialize WebSocket server with a given IP endpoint
        /// </summary>
        /// <param name="endpoint">IP endpoint</param>
        public WsServer(IPEndPoint endpoint) : base(endpoint) { webSocket = new WebSocket(this); }

        public virtual bool CloseAll(int status)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, false, null, 0, 0, status);
                return base.DisconnectAll();
            }
        }

        #region WebSocket multicast text methods

        public bool MulticastText(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, buffer, offset, size);
                return base.Multicast(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool MulticastText(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.Multicast(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket multicast binary methods

        public bool MulticastBinary(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, buffer, offset, size);
                return base.Multicast(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool MulticastBinary(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.Multicast(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket multicast ping methods

        public bool SendPing(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, buffer, offset, size);
                return base.Multicast(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendPing(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.Multicast(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        #region WebSocket multicast pong methods

        public bool SendPong(byte[] buffer, long offset, long size)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, buffer, offset, size);
                return base.Multicast(webSocket.wsSendBuffer.ToArray());
            }
        }

        public bool SendPong(string text)
        {
            lock (webSocket.wsSendLock)
            {
                webSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, true, Encoding.UTF8.GetBytes(text), 0, text.Length);
                return base.Multicast(webSocket.wsSendBuffer.ToArray());
            }
        }

        #endregion

        protected override TcpSession CreateSession() { return new WsSession(this); }
    }
}
