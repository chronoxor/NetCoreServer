using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Net.Sockets;

namespace NetCoreServer
{
    /// <summary>
    /// WebSocket utility class
    /// </summary>
    public class WebSocket : IWebSocket
    {
        public WebSocket(IWebSocket wsHandlers) { _wsHandlers = wsHandlers; ClearWSBuffers(); }

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
                        _wsHandlers.OnWSError(SocketError.InvalidArgument); //"Invalid WebSocket handshaked response: 'Connection' header value must be 'Upgrade'"
                        break;
                    }

                    connection = true;
                }
                else if (key == "Upgrade")
                {
                    if (value != "websocket")
                    {
                        error = true;
                        _wsHandlers.OnWSError(SocketError.InvalidArgument); //"Invalid WebSocket handshaked response: 'Upgrade' header value must be 'websocket'"
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
                    if (string.Compare(wskey, wshash) != 0)
                    {
                        error = true;
                        _wsHandlers.OnWSError(SocketError.InvalidArgument); //"Invalid WebSocket handshaked response: 'Sec-WebSocket-Accept' value validation failed"
                        break;
                    }

                    accept = true;
                }
            }
            
            // Failed to perfrom WebSocket handshake
            if (!accept || !connection || !upgrade)
            {
                if (!error)
                    _wsHandlers.OnWSError(SocketError.InvalidArgument); //"Invalid WebSocket response"
                return false;
            }

            // WebSocket successfully handshaked!
            wsHandshaked = true;
            Random rnd = new Random();
            rnd.NextBytes(_wsSendMask);
            _wsHandlers.OnWSConnected(response);

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
                    string wshash;
                    using (SHA1Managed sha1 = new SHA1Managed())
                    {
                        wshash = Encoding.UTF8.GetString(sha1.ComputeHash(Encoding.UTF8.GetBytes(wskey)));
                    }

                    accept = Convert.ToBase64String(Encoding.UTF8.GetBytes(wshash));
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

            // Failed to perfrom WebSocket handshake
            if (!connection || !upgrade || !wsKey || !wsVersion)
            {
                if (!error)
                    response.MakeErrorResponse("Invalid WebSocket response", 400);
                _wsHandlers.SendResponse(response);
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
            if (!_wsHandlers.OnWSConnecting(request, response))
                return false;

            // Send WebSocket upgrade response
            _wsHandlers.SendResponse(response);

            // WebSocket successfully handshaked!
            wsHandshaked = true;
            Random rnd = new Random();
            rnd.NextBytes(_wsSendMask);
            _wsHandlers.OnWSConnected(request);

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
        /// <param name="status">WebSocket status (defualt is 0)</param>
        public void PrepareSendFrame(byte opcode, bool mask, byte[] buffer, long offset, long size, int status = 0)
        {
            // Clear the previous WebSocket send buffer
            wsSendBuffer.Clear();

            // Append WebSocket frame opcode
            wsSendBuffer.Add(opcode);

            // Append WebSocket frame size
            if (size <= 125)
                wsSendBuffer.Add((byte)(((int)size & 0xFF) | (mask ? 0x80 : 0)));
            else if (size <= 65535)
            {
                wsSendBuffer.Add((byte)(126 | (mask ? 0x80 : 0)));
                wsSendBuffer.Add((byte)((size >> 8) & 0xFF));
                wsSendBuffer.Add((byte)(size & 0xFF));
            }
            else
            {
                wsSendBuffer.Add((byte)(127 | (mask ? 0x80 : 0)));
                for (int i = 7; i >= 0; --i)
                    wsSendBuffer.Add((byte)((size >> (8 * i)) & 0xFF));
            }

            if (mask)
            {
                // Append WebSocket frame mask
                wsSendBuffer.Add(_wsSendMask[0]);
                wsSendBuffer.Add(_wsSendMask[1]);
                wsSendBuffer.Add(_wsSendMask[2]);
                wsSendBuffer.Add(_wsSendMask[3]);
            }

            // Resize WebSocket frame buffer
            int bufferOffset = wsSendBuffer.Count;
            wsSendBuffer.AddRange(new byte[size]);

            // Mask WebSocket frame content
            for (int i = 0; i < size; ++i)
                wsSendBuffer[bufferOffset + i] = (byte) (buffer[offset + i] ^ _wsSendMask[i % 4]);
        }

        /// <summary>
        /// Prepare WebSocket send frame
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        public void PrepareReceiveFrame(byte[] buffer, long offset, long size)
        {
            var index = 0;

            // Clear received data after WebSocket frame was processed
            if (wsReceived)
            {
                wsReceived = false;
                wsHeaderSize = 0;
                wsPayloadSize = 0;
                wsReceiveBuffer.Clear();
                Array.Clear(_wsReceiveMask, 0, _wsReceiveMask.Length);
            }

            while (size > 0)
            {
                // Clear received data after WebSocket frame was processed
                if (wsReceived)
                {
                    wsReceived = false;
                    wsHeaderSize = 0;
                    wsPayloadSize = 0;
                    wsReceiveBuffer.Clear();
                    Array.Clear(_wsReceiveMask, 0, _wsReceiveMask.Length);
                }

                // Prepare WebSocket frame opcode and mask flag
                if (wsReceiveBuffer.Count < 2)
                {
                    for (int i = 0; i < 2; ++i, ++index, --size)
                    {
                        if (size == 0)
                            return;
                        wsReceiveBuffer.Add(buffer[offset + index]);
                    }
                }

                byte opcode = (byte) (wsReceiveBuffer[0] & 0x0F);
                bool fin = ((wsReceiveBuffer[0] >> 7) & 0x01) != 0;
                bool mask = ((wsReceiveBuffer[1] >> 7) & 0x01) != 0;
                int payload = wsReceiveBuffer[1] & (~0x80);

                // Prepare WebSocket frame size
                if (payload <= 125)
                {
                    wsHeaderSize = 2 + (mask ? 4 : 0);
                    wsPayloadSize = payload;
                    wsReceiveBuffer.Capacity = wsHeaderSize + wsPayloadSize;
                }
                else if (payload == 126)
                {
                    if (wsReceiveBuffer.Count < 4)
                    {
                        for (int i = 0; i < 2; ++i, ++index, --size)
                        {
                            if (size == 0)
                                return;
                            wsReceiveBuffer.Add(buffer[offset + index]);
                        }
                    }

                    payload = ((wsReceiveBuffer[2] << 8) | (wsReceiveBuffer[3] << 0));
                    wsHeaderSize = 4 + (mask ? 4 : 0);
                    wsPayloadSize = payload;
                    wsReceiveBuffer.Capacity = wsHeaderSize + wsPayloadSize;
                }
                else if (payload == 127)
                {
                    if (wsReceiveBuffer.Count < 10)
                    {
                        for (int i = 0; i < 8; ++i, ++index, --size)
                        {
                            if (size == 0)
                                return;
                            wsReceiveBuffer.Add(buffer[offset + index]);
                        }
                    }

                    payload = ((wsReceiveBuffer[2] << 56) | (wsReceiveBuffer[3] << 48) | (wsReceiveBuffer[4] << 40) | (wsReceiveBuffer[5] << 32) | (wsReceiveBuffer[6] << 24) | (wsReceiveBuffer[7] << 16) | (wsReceiveBuffer[8] << 8) | (wsReceiveBuffer[9] << 0));
                    wsHeaderSize = 10 + (mask ? 4 : 0);
                    wsPayloadSize = payload;
                    wsReceiveBuffer.Capacity = wsHeaderSize + wsPayloadSize;
                }

                // Prepare WebSocket frame mask
                if (mask)
                {
                    if (wsReceiveBuffer.Count < wsHeaderSize)
                    {
                        for (int i = 0; i < 4; ++i, ++index, --size)
                        {
                            if (size == 0)
                                return;
                            wsReceiveBuffer.Add(buffer[offset + index]);
                            _wsReceiveMask[i] = buffer[offset + index];
                        }
                    }
                }

                int total = wsHeaderSize + wsPayloadSize;
                int length = Math.Min(total - wsReceiveBuffer.Count, (int)size);

                // Prepare WebSocket frame payload
                wsReceiveBuffer.AddRange(buffer[((int)offset + index)..((int)offset + index + length)]);
                index += length;
                size -= length;

                // Process WebSocket frame
                if (wsReceiveBuffer.Count == total)
                {
                    int bufferOffset = wsHeaderSize;

                    // Unmask WebSocket frame content
                    if (mask)
                        for (int i = 0; i < wsPayloadSize; ++i)
                            wsReceiveBuffer[bufferOffset + i] ^= _wsReceiveMask[i % 4];

                    wsReceived = true;

                    if ((opcode & WS_PING) == WS_PING)
                    {
                        // Call the WebSocket ping handler
                        _wsHandlers.OnWSPing(wsReceiveBuffer.ToArray(), bufferOffset, wsPayloadSize);
                    }
                    else if ((opcode & WS_PONG) == WS_PONG)
                    {
                        // Call the WebSocket pong handler
                        _wsHandlers.OnWSPong(wsReceiveBuffer.ToArray(), bufferOffset, wsPayloadSize);
                    }
                    else if ((opcode & WS_CLOSE) == WS_CLOSE)
                    {
                        // Call the WebSocket close handler
                        _wsHandlers.OnWSClose(wsReceiveBuffer.ToArray(), bufferOffset, wsPayloadSize);
                    }
                    else if (((opcode & WS_TEXT) == WS_TEXT) || ((opcode & WS_BINARY) == WS_BINARY))
                    {
                        // Call the WebSocket received handler
                        _wsHandlers.OnWSReceived(wsReceiveBuffer.ToArray(), bufferOffset, wsPayloadSize);
                    }
                }
            }
        }

        /// <summary>
        /// Required WebSocket receive frame size
        /// </summary>
        public int RequiredReceiveFrameSize()
        {
            if (wsReceived)
                return 0;

            // Required WebSocket frame opcode and mask flag
            if (wsReceiveBuffer.Count < 2)
                return 2 - wsReceiveBuffer.Count;

            bool mask = ((wsReceiveBuffer[1] >> 7) & 0x01) != 0;
            int payload = wsReceiveBuffer[1] & (~0x80);

            // Required WebSocket frame size
            if ((payload == 126) && (wsReceiveBuffer.Count < 4))
                return 4 - wsReceiveBuffer.Count;
            if ((payload == 127) && (wsReceiveBuffer.Count < 10))
                return 10 - wsReceiveBuffer.Count;

            // Required WebSocket frame mask
            if ((mask) && (wsReceiveBuffer.Count < wsHeaderSize))
                return wsHeaderSize - wsReceiveBuffer.Count;

            // Required WebSocket frame payload
            return wsHeaderSize + wsPayloadSize - wsReceiveBuffer.Count;
        }

        /// <summary>
        /// Clear WebSocket send/receive buffers
        /// </summary>
        public void ClearWSBuffers()
        {
            wsReceived = false;
            wsHeaderSize = 0;
            wsPayloadSize = 0;
            wsReceiveBuffer.Clear();
            Array.Clear(_wsReceiveMask, 0, _wsReceiveMask.Length);

            lock(wsSendLock)
            {
                wsSendBuffer.Clear();
                Array.Clear(_wsSendMask, 0, _wsSendMask.Length);
            }
        }

        /// <summary>
        /// Handshaked flag
        /// </summary>
        internal bool wsHandshaked = false;
        /// <summary>
        /// Received frame flag
        /// </summary>
        internal bool wsReceived = false;
        /// <summary>
        /// Received frame header size
        /// </summary>
        internal int wsHeaderSize = 0;
        /// <summary>
        /// Received frame payload size
        /// </summary>
        internal int wsPayloadSize = 0;
        /// <summary>
        /// Receive buffer
        /// </summary>
        internal List<byte> wsReceiveBuffer = new List<byte>();
        /// <summary>
        /// Receive mask
        /// </summary>
        private byte[] _wsReceiveMask = new byte[4];

        /// <summary>
        /// Send buffer lock
        /// </summary>
        internal readonly object wsSendLock = new object();
        /// <summary>
        /// Send buffer
        /// </summary>
        internal List<byte> wsSendBuffer = new List<byte>();
        /// <summary>
        /// Send mask
        /// </summary>
        private byte[] _wsSendMask = new byte[4];

        private IWebSocket _wsHandlers;
    }
}
    

