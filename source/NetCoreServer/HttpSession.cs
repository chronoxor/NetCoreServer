namespace NetCoreServer
{
    /// <summary>
    /// HTTP session is used to receive/send HTTP requests/responses from the connected HTTP client.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    public class HttpSession : TcpSession
    {
        public HttpSession(HttpServer server) : base(server)
        {
            Cache = server.Cache;
            Request = new HttpRequest();
            Response = new HttpResponse();
        }

        /// <summary>
        /// Get the static content cache
        /// </summary>
        public FileCache Cache { get; }

        /// <summary>
        /// Get the HTTP request
        /// </summary>
        protected HttpRequest Request { get; }

        /// <summary>
        /// Get the HTTP response
        /// </summary>
        public HttpResponse Response { get; }

        #region Send response / Send response body

        /// <summary>
        /// Send the current HTTP response (synchronous)
        /// </summary>
        /// <returns>Size of sent data</returns>
        public long SendResponse() { return SendResponse(Response); }
        /// <summary>
        /// Send the HTTP response (synchronous)
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <returns>Size of sent data</returns>
        public long SendResponse(HttpResponse response) { return Send(response.Cache.Data, response.Cache.Offset, response.Cache.Size); }

        /// <summary>
        /// Send the HTTP response body (synchronous)
        /// </summary>
        /// <param name="body">HTTP response body</param>
        /// <returns>Size of sent data</returns>
        public long SendResponseBody(string body) { return Send(body); }
        /// <summary>
        /// Send the HTTP response body (synchronous)
        /// </summary>
        /// <param name="buffer">HTTP response body buffer</param>
        /// <returns>Size of sent data</returns>
        public long SendResponseBody(byte[] buffer) { return Send(buffer); }
        /// <summary>
        /// Send the HTTP response body (synchronous)
        /// </summary>
        /// <param name="buffer">HTTP response body buffer</param>
        /// <param name="offset">HTTP response body buffer offset</param>
        /// <param name="size">HTTP response body size</param>
        /// <returns>Size of sent data</returns>
        public long SendResponseBody(byte[] buffer, long offset, long size) { return Send(buffer, offset, size); }

        /// <summary>
        /// Send the current HTTP response (asynchronous)
        /// </summary>
        /// <returns>'true' if the current HTTP response was successfully sent, 'false' if the session is not connected</returns>
        public bool SendResponseAsync() { return SendResponseAsync(Response); }
        /// <summary>
        /// Send the HTTP response (asynchronous)
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <returns>'true' if the current HTTP response was successfully sent, 'false' if the session is not connected</returns>
        public bool SendResponseAsync(HttpResponse response) { return SendAsync(response.Cache.Data, response.Cache.Offset, response.Cache.Size); }

        /// <summary>
        /// Send the HTTP response body (asynchronous)
        /// </summary>
        /// <param name="body">HTTP response body</param>
        /// <returns>'true' if the HTTP response body was successfully sent, 'false' if the session is not connected</returns>
        public bool SendResponseBodyAsync(string body) { return SendAsync(body); }
        /// <summary>
        /// Send the HTTP response body (asynchronous)
        /// </summary>
        /// <param name="buffer">HTTP response body buffer</param>
        /// <returns>'true' if the HTTP response body was successfully sent, 'false' if the session is not connected</returns>
        public bool SendResponseBodyAsync(byte[] buffer) { return SendAsync(buffer); }
        /// <summary>
        /// Send the HTTP response body (asynchronous)
        /// </summary>
        /// <param name="buffer">HTTP response body buffer</param>
        /// <param name="offset">HTTP response body buffer offset</param>
        /// <param name="size">HTTP response body size</param>
        /// <returns>'true' if the HTTP response body was successfully sent, 'false' if the session is not connected</returns>
        public bool SendResponseBodyAsync(byte[] buffer, long offset, long size) { return SendAsync(buffer, offset, size); }

        #endregion

        #region Session handlers

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Receive HTTP request header
            if (Request.IsPendingHeader())
            {
                if (Request.ReceiveHeader(buffer, (int)offset, (int)size))
                    OnReceivedRequestHeader(Request);

                size = 0;
            }

            // Check for HTTP request error
            if (Request.IsErrorSet)
            {
                OnReceivedRequestError(Request, "Invalid HTTP request!");
                Request.Clear();
                Disconnect();
                return;
            }

            // Receive HTTP request body
            if (Request.ReceiveBody(buffer, (int)offset, (int)size))
            {
                OnReceivedRequestInternal(Request);
                Request.Clear();
                return;
            }

            // Check for HTTP request error
            if (Request.IsErrorSet)
            {
                OnReceivedRequestError(Request, "Invalid HTTP request!");
                Request.Clear();
                Disconnect();
                return;
            }
        }

        protected override void OnDisconnected()
        {
            // Receive HTTP request body
            if (Request.IsPendingBody())
            {
                OnReceivedRequestInternal(Request);
                Request.Clear();
                return;
            }
        }

        /// <summary>
        /// Handle HTTP request header received notification
        /// </summary>
        /// <remarks>Notification is called when HTTP request header was received from the client.</remarks>
        /// <param name="request">HTTP request</param>
        protected virtual void OnReceivedRequestHeader(HttpRequest request) {}

        /// <summary>
        /// Handle HTTP request received notification
        /// </summary>
        /// <remarks>Notification is called when HTTP request was received from the client.</remarks>
        /// <param name="request">HTTP request</param>
        protected virtual void OnReceivedRequest(HttpRequest request) {}

        /// <summary>
        /// Handle HTTP request error notification
        /// </summary>
        /// <remarks>Notification is called when HTTP request error was received from the client.</remarks>
        /// <param name="request">HTTP request</param>
        /// <param name="error">HTTP request error</param>
        protected virtual void OnReceivedRequestError(HttpRequest request, string error) {}

        #endregion

        private void OnReceivedRequestInternal(HttpRequest request)
        {
            // Try to get the cached response
            if (request.Method == "GET")
            {
                var response = Cache.Find(request.Url);
                if (response.Item1)
                {
                    SendAsync(response.Item2);
                    return;
                }
            }

            // Process the request
            OnReceivedRequest(request);
        }
    }
}
