using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace NetCoreServer
{
    /// <summary>
    /// WebSocket utility class
    /// </summary>
    public class WebSocket : IWebSocket
    {
        private readonly IWebSocket _wsHandler;

        public WebSocket(IWebSocket wsHandler) { _wsHandler = wsHandler; ClearWsBuffers(); }

        /// <summary>
        /// Final frame
        /// </summary>
        public const byte WS_FIN = 0x80;
        /// <summary>
        /// Text frame
        /// </summary>
        public const byte WS_TEXT = 0x01;
        /// <summary>
        /// Binary frame
        /// </summary>
        public const byte WS_BINARY = 0x02;
        /// <summary>
        /// Close frame
        /// </summary>
        public const byte WS_CLOSE = 0x08;
        /// <summary>
        /// Ping frame
        /// </summary>
        public const byte WS_PING = 0x09;
        /// <summary>
        /// Pong frame
        /// </summary>
        public const byte WS_PONG = 0x0A;

        /// <summary>
        /// Perform WebSocket client upgrade
        /// </summary>
        /// <param name="response">WebSocket upgrade HTTP response</param>
        /// <param name="id">WebSocket client Id</param>
        /// <returns>'true' if the WebSocket was successfully upgrade, 'false' if the WebSocket was not upgrade</returns>
        public bool PerformClientUpgrade(HttpResponse response, Guid id)
        {
            if (response.Status != 101)
                return false;

            bool error = false;
            bool accept = false;
            bool connection = false;
            bool upgrade = false;

            // Validate WebSocket handshake headers
            for (int i = 0; i < response.Headers; ++i)
            {
                var header = response.Header(i);
                var key = header.Item1;
                var value = header.Item2;

                if (key == "Connection")
                {
                    if (value != "Upgrade")
                    {
                        error = true;
                        _wsHandler.OnWsError("Invalid WebSocket handshaked response: 'Connection' header value must be 'Upgrade'");
                        break;
                    }

                    connection = true;
                }
                else if (key == "Upgrade")
                {
                    if (value != "websocket")
                    {
                        error = true;
                        _wsHandler.OnWsError("Invalid WebSocket handshaked response: 'Upgrade' header value must be 'websocket'");
                        break;
                    }

                    upgrade = true;
                }
                else if (key == "Sec-WebSocket-Accept")
                {
                    // Calculate the original WebSocket hash
                    string wskey = Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString())) + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    string wshash;
                    using (SHA1Managed sha1 = new SHA1Managed())
                    {
                        wshash = Encoding.UTF8.GetString(sha1.ComputeHash(Encoding.UTF8.GetBytes(wskey)));
                    }

                    // Get the received WebSocket hash
                    wskey = Encoding.UTF8.GetString(Convert.FromBase64String(value));

                    // Compare original and received hashes
                    if (string.Compare(wskey, wshash, StringComparison.InvariantCulture) != 0)
                    {
                        error = true;
                        _wsHandler.OnWsError("Invalid WebSocket handshaked response: 'Sec-WebSocket-Accept' value validation failed");
                        break;
                    }

                    accept = true;
                }
            }

            // Failed to perform WebSocket handshake
            if (!accept || !connection || !upgrade)
            {
                if (!error)
                    _wsHandler.OnWsError("Invalid WebSocket response");
                return false;
            }

            // WebSocket successfully handshaked!
            WsHandshaked = true;
            Random rnd = new Random();
            rnd.NextBytes(WsSendMask);
            _wsHandler.OnWsConnected(response);

            return true;
        }

        /// <summary>
        /// Perform WebSocket server upgrade
        /// </summary>
        /// <param name="request">WebSocket upgrade HTTP request</param>
        /// <param name="response">WebSocket upgrade HTTP response</param>
        /// <returns>'true' if the WebSocket was successfully upgrade, 'false' if the WebSocket was not upgrade</returns>
        public bool PerformServerUpgrade(HttpRequest request, HttpResponse response)
        {
            if (request.Method != "GET")
                return false;

            bool error = false;
            bool connection = false;
            bool upgrade = false;
            bool wsKey = false;
            bool wsVersion = false;

            string accept = "";

            // Validate WebSocket handshake headers
            for (int i = 0; i < request.Headers; ++i)
            {
                var header = request.Header(i);
                var key = header.Item1;
                var value = header.Item2;

                if (key == "Connection")
                {
                    if ((value != "Upgrade") && (value != "keep-alive, Upgrade"))
                    {
                        error = true;
                        response.MakeErrorResponse("Invalid WebSocket handshaked request: 'Connection' header value must be 'Upgrade' or 'keep-alive, Upgrade'", 400);
                        break;
                    }

                    connection = true;
                }
                else if (key == "Upgrade")
                {
                    if (value != "websocket")
                    {
                        error = true;
                        response.MakeErrorResponse("Invalid WebSocket handshaked request: 'Upgrade' header value must be 'websocket'", 400);
                        break;
                    }

                    upgrade = true;
                }
                else if (key == "Sec-WebSocket-Key")
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        error = true;
                        response.MakeErrorResponse("Invalid WebSocket handshaked request: 'Sec-WebSocket-Key' header value must be non empty", 400);
                        break;
                    }

                    // Calculate the original WebSocket hash
                    string wskey = value + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    byte[] wshash;
                    using (SHA1Managed sha1 = new SHA1Managed())
                    {
                        wshash = sha1.ComputeHash(Encoding.UTF8.GetBytes(wskey));
                    }

                    accept = Convert.ToBase64String(wshash);

                    wsKey = true;
                }
                else if (key == "Sec-WebSocket-Version")
                {
                    if (value != "13")
                    {
                        error = true;
                        response.MakeErrorResponse("Invalid WebSocket handshaked request: 'Sec-WebSocket-Version' header value must be '13'", 400);
                        break;
                    }

                    wsVersion = true;
                }
            }

            // Filter out non WebSocket handshake requests
            if (!connection && !upgrade && !wsKey && !wsVersion)
                return false;

            // Failed to perform WebSocket handshake
            if (!connection || !upgrade || !wsKey || !wsVersion)
            {
                if (!error)
                    response.MakeErrorResponse("Invalid WebSocket response", 400);
                _wsHandler.SendResponse(response);
                return false;
            }

            // Prepare WebSocket upgrade success response
            response.Clear();
            response.SetBegin(101);
            response.SetHeader("Connection", "Upgrade");
            response.SetHeader("Upgrade", "websocket");
            response.SetHeader("Sec-WebSocket-Accept", accept);
            response.SetBody();

            // Validate WebSocket upgrade request and response
            if (!_wsHandler.OnWsConnecting(request, response))
                return false;

            // Send WebSocket upgrade response
            _wsHandler.SendResponse(response);

            // WebSocket successfully handshaked!
            WsHandshaked = true;
            for (int i = 0; i < WsSendMask.Length; i++)
                WsSendMask[i] = 0;
            _wsHandler.OnWsConnected(request);

            return true;
        }

        /// <summary>
        /// Prepare WebSocket send frame
        /// </summary>
        /// <param name="opcode">WebSocket opcode</param>
        /// <param name="mask">WebSocket mask</param>
        /// <param name="buffer">Buffer to send</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <param name="status">WebSocket status (default is 0)</param>
        public void PrepareSendFrame(byte opcode, bool mask, byte[] buffer, long offset, long size, int status = 0)
        {
            // Clear the previous WebSocket send buffer
            WsSendBuffer.Clear();

            // Append WebSocket frame opcode
            WsSendBuffer.Add(opcode);

            // Append WebSocket frame size
            if (size <= 125)
                WsSendBuffer.Add((byte)(((int)size & 0xFF) | (mask ? 0x80 : 0)));
            else if (size <= 65535)
            {
                WsSendBuffer.Add((byte)(126 | (mask ? 0x80 : 0)));
                WsSendBuffer.Add((byte)((size >> 8) & 0xFF));
                WsSendBuffer.Add((byte)(size & 0xFF));
            }
            else
            {
                WsSendBuffer.Add((byte)(127 | (mask ? 0x80 : 0)));
                for (int i = 7; i >= 0; --i)
                    WsSendBuffer.Add((byte)((size >> (8 * i)) & 0xFF));
            }

            if (mask)
            {
                // Append WebSocket frame mask
                WsSendBuffer.Add(WsSendMask[0]);
                WsSendBuffer.Add(WsSendMask[1]);
                WsSendBuffer.Add(WsSendMask[2]);
                WsSendBuffer.Add(WsSendMask[3]);
            }

            // Resize WebSocket frame buffer
            int bufferOffset = WsSendBuffer.Count;
            WsSendBuffer.AddRange(new byte[size]);

            // Mask WebSocket frame content
            for (int i = 0; i < size; ++i)
                WsSendBuffer[bufferOffset + i] = (byte) (buffer[offset + i] ^ WsSendMask[i % 4]);
        }

        /// <summary>
        /// Prepare WebSocket send frame
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        public void PrepareReceiveFrame(byte[] buffer, long offset, long size)
        {
            lock (WsReceiveLock)
            {
                var index = 0;

                // Clear received data after WebSocket frame was processed
                if (WsReceived)
                {
                    WsReceived = false;
                    WsHeaderSize = 0;
                    WsPayloadSize = 0;
                    WsReceiveBuffer.Clear();
                    Array.Clear(WsReceiveMask, 0, WsReceiveMask.Length);
                }

                while (size > 0)
                {
                    // Clear received data after WebSocket frame was processed
                    if (WsReceived)
                    {
                        WsReceived = false;
                        WsHeaderSize = 0;
                        WsPayloadSize = 0;
                        WsReceiveBuffer.Clear();
                        Array.Clear(WsReceiveMask, 0, WsReceiveMask.Length);
                    }

                    // Prepare WebSocket frame opcode and mask flag
                    if (WsReceiveBuffer.Count < 2)
                    {
                        for (int i = 0; i < 2; ++i, ++index, --size)
                        {
                            if (size == 0)
                                return;
                            WsReceiveBuffer.Add(buffer[offset + index]);
                        }
                    }

                    byte opcode = (byte) (WsReceiveBuffer[0] & 0x0F);
                    bool fin = ((WsReceiveBuffer[0] >> 7) & 0x01) != 0;
                    bool mask = ((WsReceiveBuffer[1] >> 7) & 0x01) != 0;
                    int payload = WsReceiveBuffer[1] & (~0x80);

                    // Prepare WebSocket frame size
                    if (payload <= 125)
                    {
                        WsHeaderSize = 2 + (mask ? 4 : 0);
                        WsPayloadSize = payload;
                    WsReceiveBuffer.Capacity = WsHeaderSize + WsPayloadSize;
                    }
                    else if (payload == 126)
                    {
                        if (WsReceiveBuffer.Count < 4)
                        {
                            for (int i = 0; i < 2; ++i, ++index, --size)
                            {
                                if (size == 0)
                                    return;
                                WsReceiveBuffer.Add(buffer[offset + index]);
                            }
                        }

                        payload = ((WsReceiveBuffer[2] << 8) | (WsReceiveBuffer[3] << 0));
                        WsHeaderSize = 4 + (mask ? 4 : 0);
                        WsPayloadSize = payload;
                    WsReceiveBuffer.Capacity = WsHeaderSize + WsPayloadSize;
                    }
                    else if (payload == 127)
                    {
                        if (WsReceiveBuffer.Count < 10)
                        {
                            for (int i = 0; i < 8; ++i, ++index, --size)
                            {
                                if (size == 0)
                                    return;
                                WsReceiveBuffer.Add(buffer[offset + index]);
                            }
                        }

                        payload = ((WsReceiveBuffer[2] << 56) | (WsReceiveBuffer[3] << 48) | (WsReceiveBuffer[4] << 40) | (WsReceiveBuffer[5] << 32) | (WsReceiveBuffer[6] << 24) | (WsReceiveBuffer[7] << 16) | (WsReceiveBuffer[8] << 8) | (WsReceiveBuffer[9] << 0));
                        WsHeaderSize = 10 + (mask ? 4 : 0);
                        WsPayloadSize = payload;
                    WsReceiveBuffer.Capacity = WsHeaderSize + WsPayloadSize;
                    }

                    // Prepare WebSocket frame mask
                    if (mask)
                    {
                        if (WsReceiveBuffer.Count < WsHeaderSize)
                        {
                            for (int i = 0; i < 4; ++i, ++index, --size)
                            {
                                if (size == 0)
                                    return;
                                WsReceiveBuffer.Add(buffer[offset + index]);
                                WsReceiveMask[i] = buffer[offset + index];
                            }
                        }
                    }

                    int total = WsHeaderSize + WsPayloadSize;
                    int length = Math.Min(total - WsReceiveBuffer.Count, (int)size);

                    // Prepare WebSocket frame payload
                    WsReceiveBuffer.AddRange(buffer.Skip((int)offset + index).Take(length));
                    index += length;
                    size -= length;

                    // Process WebSocket frame
                    if (WsReceiveBuffer.Count == total)
                    {
                        int bufferOffset = WsHeaderSize;

                        // Unmask WebSocket frame content
                        if (mask)
                            for (int i = 0; i < WsPayloadSize; ++i)
                                WsReceiveBuffer[bufferOffset + i] ^= WsReceiveMask[i % 4];

                        WsReceived = true;

                        if ((opcode & WS_PING) == WS_PING)
                        {
                            // Call the WebSocket ping handler
                            _wsHandler.OnWsPing(WsReceiveBuffer.ToArray(), bufferOffset, WsPayloadSize);
                        }
                        else if ((opcode & WS_PONG) == WS_PONG)
                        {
                            // Call the WebSocket pong handler
                            _wsHandler.OnWsPong(WsReceiveBuffer.ToArray(), bufferOffset, WsPayloadSize);
                        }
                        else if ((opcode & WS_CLOSE) == WS_CLOSE)
                        {
                            // Call the WebSocket close handler
                            _wsHandler.OnWsClose(WsReceiveBuffer.ToArray(), bufferOffset, WsPayloadSize);
                        }
                        else if (((opcode & WS_TEXT) == WS_TEXT) || ((opcode & WS_BINARY) == WS_BINARY))
                        {
                            // Call the WebSocket received handler
                            _wsHandler.OnWsReceived(WsReceiveBuffer.ToArray(), bufferOffset, WsPayloadSize);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Required WebSocket receive frame size
        /// </summary>
        public int RequiredReceiveFrameSize()
        {
            lock (WsReceiveLock)
            {
                if (WsReceived)
                    return 0;

                // Required WebSocket frame opcode and mask flag
                if (WsReceiveBuffer.Count < 2)
                    return 2 - WsReceiveBuffer.Count;

                bool mask = ((WsReceiveBuffer[1] >> 7) & 0x01) != 0;
                int payload = WsReceiveBuffer[1] & (~0x80);

                // Required WebSocket frame size
                if ((payload == 126) && (WsReceiveBuffer.Count < 4))
                    return 4 - WsReceiveBuffer.Count;
                if ((payload == 127) && (WsReceiveBuffer.Count < 10))
                    return 10 - WsReceiveBuffer.Count;

                // Required WebSocket frame mask
                if ((mask) && (WsReceiveBuffer.Count < WsHeaderSize))
                    return WsHeaderSize - WsReceiveBuffer.Count;

                // Required WebSocket frame payload
                return WsHeaderSize + WsPayloadSize - WsReceiveBuffer.Count;
            }
        }

        /// <summary>
        /// Clear WebSocket send/receive buffers
        /// </summary>
        public void ClearWsBuffers()
        {
            lock (WsReceiveLock)
            {
                WsReceived = false;
                WsHeaderSize = 0;
                WsPayloadSize = 0;
                WsReceiveBuffer.Clear();
                Array.Clear(WsReceiveMask, 0, WsReceiveMask.Length);
            }

            lock (WsSendLock)
            {
                WsSendBuffer.Clear();
                Array.Clear(WsSendMask, 0, WsSendMask.Length);
            }
        }

        #region IWebSocket implementation

        public void OnWsConnecting(HttpRequest request) { _wsHandler.OnWsConnecting(request); }
        public void OnWsConnected(HttpResponse response) { _wsHandler.OnWsConnected(response); }
        public bool OnWsConnecting(HttpRequest request, HttpResponse response) { return _wsHandler.OnWsConnecting(request, response); }
        public void OnWsConnected(HttpRequest request) { _wsHandler.OnWsConnected(request); }
        public void OnWsDisconnected() { _wsHandler.OnWsDisconnected(); }
        public void OnWsReceived(byte[] buffer, long offset, long size) { _wsHandler.OnWsReceived(buffer, offset, size); }
        public void OnWsClose(byte[] buffer, long offset, long size) { _wsHandler.OnWsClose(buffer, offset, size); }
        public void OnWsPing(byte[] buffer, long offset, long size) { _wsHandler.OnWsPing(buffer, offset, size); }
        public void OnWsPong(byte[] buffer, long offset, long size) { _wsHandler.OnWsPong(buffer, offset, size); }
        public void OnWsError(string error) { _wsHandler.OnWsError(error); }
        public void OnWsError(SocketError error) { _wsHandler.OnWsError(error); }
        public void SendResponse(HttpResponse response) { _wsHandler.SendResponse(response); }

        #endregion

        /// <summary>
        /// Handshaked flag
        /// </summary>
        internal bool WsHandshaked;
        /// <summary>
        /// Received frame flag
        /// </summary>
        internal bool WsReceived;
        /// <summary>
        /// Received frame header size
        /// </summary>
        internal int WsHeaderSize;
        /// <summary>
        /// Received frame payload size
        /// </summary>
        internal int WsPayloadSize;

        /// <summary>
        /// Receive buffer lock
        /// </summary>
        internal readonly object WsReceiveLock = new object();
        /// <summary>
        /// Receive buffer
        /// </summary>
        internal readonly List<byte> WsReceiveBuffer = new List<byte>();
        /// <summary>
        /// Receive mask
        /// </summary>
        internal readonly byte[] WsReceiveMask = new byte[4];

        /// <summary>
        /// Send buffer lock
        /// </summary>
        internal readonly object WsSendLock = new object();
        /// <summary>
        /// Send buffer
        /// </summary>
        internal readonly List<byte> WsSendBuffer = new List<byte>();
        /// <summary>
        /// Send mask
        /// </summary>
        internal readonly byte[] WsSendMask = new byte[4];
    }
}
