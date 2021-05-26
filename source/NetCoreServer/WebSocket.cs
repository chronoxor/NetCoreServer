using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace NetCoreServer
{
    /// <summary>
    /// WebSocket utility class
    /// </summary>
    public class WebSocket : IWebSocket
    {
        private readonly IWebSocket _wsHandler;

        public WebSocket(IWebSocket wsHandler) { _wsHandler = wsHandler; ClearWsBuffers(); InitWsNonce(); }

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
            for (int i = 0; i < response.Headers; i++)
            {
                var header = response.Header(i);
                var key = header.Item1;
                var value = header.Item2;

                if (string.Compare(key, "Connection", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (string.Compare(value, "Upgrade", StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        error = true;
                        _wsHandler.OnWsError("Invalid WebSocket handshaked response: 'Connection' header value must be 'Upgrade'");
                        break;
                    }

                    connection = true;
                }
                else if (string.Compare(key, "Upgrade", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (string.Compare(value, "websocket", StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        error = true;
                        _wsHandler.OnWsError("Invalid WebSocket handshaked response: 'Upgrade' header value must be 'websocket'");
                        break;
                    }

                    upgrade = true;
                }
                else if (string.Compare(key, "Sec-WebSocket-Accept", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // Calculate the original WebSocket hash
                    string wskey = Convert.ToBase64String(WsNonce) + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
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
            WsRandom.NextBytes(WsSendMask);
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
            for (int i = 0; i < request.Headers; i++)
            {
                var header = request.Header(i);
                var key = header.Item1;
                var value = header.Item2;

                if (string.Compare(key, "Connection", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if ((string.Compare(value, "Upgrade", StringComparison.OrdinalIgnoreCase) != 0) && (string.Compare(value, "keep-alive, Upgrade", StringComparison.OrdinalIgnoreCase) != 0))
                    {
                        error = true;
                        response.MakeErrorResponse(400, "Invalid WebSocket handshaked request: 'Connection' header value must be 'Upgrade' or 'keep-alive, Upgrade'");
                        break;
                    }

                    connection = true;
                }
                else if (string.Compare(key, "Upgrade", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (string.Compare(value, "websocket", StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        error = true;
                        response.MakeErrorResponse(400, "Invalid WebSocket handshaked request: 'Upgrade' header value must be 'websocket'");
                        break;
                    }

                    upgrade = true;
                }
                else if (string.Compare(key, "Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        error = true;
                        response.MakeErrorResponse(400, "Invalid WebSocket handshaked request: 'Sec-WebSocket-Key' header value must be non empty");
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
                else if (string.Compare(key, "Sec-WebSocket-Version", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (string.Compare(value, "13", StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        error = true;
                        response.MakeErrorResponse(400, "Invalid WebSocket handshaked request: 'Sec-WebSocket-Version' header value must be '13'");
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
                    response.MakeErrorResponse(400, "Invalid WebSocket response");
                _wsHandler.SendUpgrade(response);
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
            _wsHandler.SendUpgrade(response);

            // WebSocket successfully handshaked!
            WsHandshaked = true;
            Array.Fill(WsSendMask, (byte)0);
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
                for (int i = 7; i >= 0; i--)
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
            for (int i = 0; i < size; i++)
                WsSendBuffer[bufferOffset + i] = (byte)(buffer[offset + i] ^ WsSendMask[i % 4]);
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
                if (WsFrameReceived)
                {
                    WsFrameReceived = false;
                    WsHeaderSize = 0;
                    WsPayloadSize = 0;
                    WsReceiveFrameBuffer.Clear();
                    Array.Clear(WsReceiveMask, 0, WsReceiveMask.Length);
                }
                if (WsFinalReceived)
                {
                    WsFinalReceived = false;
                    WsReceiveFinalBuffer.Clear();
                }

                while (size > 0)
                {
                    // Clear received data after WebSocket frame was processed
                    if (WsFrameReceived)
                    {
                        WsFrameReceived = false;
                        WsHeaderSize = 0;
                        WsPayloadSize = 0;
                        WsReceiveFrameBuffer.Clear();
                        Array.Clear(WsReceiveMask, 0, WsReceiveMask.Length);
                    }
                    if (WsFinalReceived)
                    {
                        WsFinalReceived = false;
                        WsReceiveFinalBuffer.Clear();
                    }

                    // Prepare WebSocket frame opcode and mask flag
                    if (WsReceiveFrameBuffer.Count < 2)
                    {
                        for (int i = 0; i < 2; i++, index++, size--)
                        {
                            if (size == 0)
                                return;
                            WsReceiveFrameBuffer.Add(buffer[offset + index]);
                        }
                    }

                    byte opcode = (byte)(WsReceiveFrameBuffer[0] & 0x0F);
                    bool fin = ((WsReceiveFrameBuffer[0] >> 7) & 0x01) != 0;
                    bool mask = ((WsReceiveFrameBuffer[1] >> 7) & 0x01) != 0;
                    int payload = WsReceiveFrameBuffer[1] & (~0x80);

                    // Prepare WebSocket opcode
                    WsOpcode = (opcode != 0) ? opcode : WsOpcode;

                    // Prepare WebSocket frame size
                    if (payload <= 125)
                    {
                        WsHeaderSize = 2 + (mask ? 4 : 0);
                        WsPayloadSize = payload;
                    }
                    else if (payload == 126)
                    {
                        if (WsReceiveFrameBuffer.Count < 4)
                        {
                            for (int i = 0; i < 2; i++, index++, size--)
                            {
                                if (size == 0)
                                    return;
                                WsReceiveFrameBuffer.Add(buffer[offset + index]);
                            }
                        }

                        payload = ((WsReceiveFrameBuffer[2] << 8) | (WsReceiveFrameBuffer[3] << 0));
                        WsHeaderSize = 4 + (mask ? 4 : 0);
                        WsPayloadSize = payload;
                    }
                    else if (payload == 127)
                    {
                        if (WsReceiveFrameBuffer.Count < 10)
                        {
                            for (int i = 0; i < 8; i++, index++, size--)
                            {
                                if (size == 0)
                                    return;
                                WsReceiveFrameBuffer.Add(buffer[offset + index]);
                            }
                        }

                        payload = ((WsReceiveFrameBuffer[2] << 56) | (WsReceiveFrameBuffer[3] << 48) | (WsReceiveFrameBuffer[4] << 40) | (WsReceiveFrameBuffer[5] << 32) | (WsReceiveFrameBuffer[6] << 24) | (WsReceiveFrameBuffer[7] << 16) | (WsReceiveFrameBuffer[8] << 8) | (WsReceiveFrameBuffer[9] << 0));
                        WsHeaderSize = 10 + (mask ? 4 : 0);
                        WsPayloadSize = payload;
                    }

                    // Prepare WebSocket frame mask
                    if (mask)
                    {
                        if (WsReceiveFrameBuffer.Count < WsHeaderSize)
                        {
                            for (int i = 0; i < 4; i++, index++, size--)
                            {
                                if (size == 0)
                                    return;
                                WsReceiveFrameBuffer.Add(buffer[offset + index]);
                                WsReceiveMask[i] = buffer[offset + index];
                            }
                        }
                    }

                    int total = WsHeaderSize + WsPayloadSize;
                    int length = Math.Min(total - WsReceiveFrameBuffer.Count, (int)size);

                    // Prepare WebSocket frame payload
                    WsReceiveFrameBuffer.AddRange(buffer[((int)offset + index)..((int)offset + index + length)]);
                    index += length;
                    size -= length;

                    // Process WebSocket frame
                    if (WsReceiveFrameBuffer.Count == total)
                    {
                        // Unmask WebSocket frame content
                        if (mask)
                        {
                            for (int i = 0; i < WsPayloadSize; i++)
                                WsReceiveFinalBuffer.Add((byte)(WsReceiveFrameBuffer[WsHeaderSize + i] ^ WsReceiveMask[i % 4]));
                        }
                        else
                            WsReceiveFinalBuffer.AddRange(WsReceiveFrameBuffer.GetRange(WsHeaderSize, WsPayloadSize));

                        WsFrameReceived = true;

                        // Finalize WebSocket frame
                        if (fin)
                        {
                            WsFinalReceived = true;

                            switch (WsOpcode)
                            {
                                case WS_PING:
                                {
                                    // Call the WebSocket ping handler
                                    _wsHandler.OnWsPing(WsReceiveFinalBuffer.ToArray(), 0, WsReceiveFinalBuffer.Count);
                                    break;
                                }
                                case WS_PONG:
                                {
                                    // Call the WebSocket pong handler
                                    _wsHandler.OnWsPong(WsReceiveFinalBuffer.ToArray(), 0, WsReceiveFinalBuffer.Count);
                                    break;
                                }
                                case WS_CLOSE:
                                {
                                    // Call the WebSocket close handler
                                    _wsHandler.OnWsClose(WsReceiveFinalBuffer.ToArray(), 0, WsReceiveFinalBuffer.Count);
                                    break;
                                }
                                case WS_BINARY:
                                case WS_TEXT:
                                {
                                    // Call the WebSocket received handler
                                    _wsHandler.OnWsReceived(WsReceiveFinalBuffer.ToArray(), 0, WsReceiveFinalBuffer.Count);
                                    break;
                                }
                            }
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
                if (WsFrameReceived)
                    return 0;

                // Required WebSocket frame opcode and mask flag
                if (WsReceiveFrameBuffer.Count < 2)
                    return 2 - WsReceiveFrameBuffer.Count;

                bool mask = ((WsReceiveFrameBuffer[1] >> 7) & 0x01) != 0;
                int payload = WsReceiveFrameBuffer[1] & (~0x80);

                // Required WebSocket frame size
                if ((payload == 126) && (WsReceiveFrameBuffer.Count < 4))
                    return 4 - WsReceiveFrameBuffer.Count;
                if ((payload == 127) && (WsReceiveFrameBuffer.Count < 10))
                    return 10 - WsReceiveFrameBuffer.Count;

                // Required WebSocket frame mask
                if ((mask) && (WsReceiveFrameBuffer.Count < WsHeaderSize))
                    return WsHeaderSize - WsReceiveFrameBuffer.Count;

                // Required WebSocket frame payload
                return WsHeaderSize + WsPayloadSize - WsReceiveFrameBuffer.Count;
            }
        }

        /// <summary>
        /// Clear WebSocket send/receive buffers
        /// </summary>
        public void ClearWsBuffers()
        {
            lock (WsReceiveLock)
            {
                WsFrameReceived = false;
                WsFinalReceived = false;
                WsHeaderSize = 0;
                WsPayloadSize = 0;
                WsReceiveFrameBuffer.Clear();
                WsReceiveFinalBuffer.Clear();
                Array.Clear(WsReceiveMask, 0, WsReceiveMask.Length);
            }

            lock (WsSendLock)
            {
                WsSendBuffer.Clear();
                Array.Clear(WsSendMask, 0, WsSendMask.Length);
            }
        }

        /// <summary>
        /// Initialize WebSocket random nonce
        /// </summary>
        public void InitWsNonce() => WsRandom.NextBytes(WsNonce);

        /// <summary>
        /// Handshaked flag
        /// </summary>
        internal bool WsHandshaked;
        /// <summary>
        /// Received frame flag
        /// </summary>
        internal bool WsFrameReceived;
        /// <summary>
        /// Received final flag
        /// </summary>
        internal bool WsFinalReceived;
        /// <summary>
        /// Received frame opcode
        /// </summary>
        internal int WsOpcode;
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
        /// Receive frame buffer
        /// </summary>
        internal readonly List<byte> WsReceiveFrameBuffer = new List<byte>();
        /// <summary>
        /// Receive final buffer
        /// </summary>
        internal readonly List<byte> WsReceiveFinalBuffer = new List<byte>();
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

        /// <summary>
        /// WebSocket random generator
        /// </summary>
        internal readonly Random WsRandom = new Random();
        /// <summary>
        /// WebSocket random nonce of 16 bytes
        /// </summary>
        internal readonly byte[] WsNonce = new byte[16];
    }
}
