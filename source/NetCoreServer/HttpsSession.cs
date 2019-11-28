using System;
using System.Collections.Generic;
using System.Text;

namespace NetCoreServer
{
    /// <summary>
    /// HTTPS session is used to receive/send HTTP requests/responses from the connected HTTPS client.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    public class HttpsSession : SslSession
    {
        public HttpsSession(HttpsServer server) : base(server)
        {
            cache = server.cache;
        }

        /// <summary>
        /// Get the static content cache
        /// </summary>
        public FileCache Cache { get { return cache; } }

        /// <summary>
        /// Get the HTTP response
        /// </summary>
        public HttpResponse Response { get { return _response; } }

        #region Send response / Send response body

        /// <summary>
        /// Send the current HTTP response (synchronous)
        /// </summary>
        /// <returns>Size of sent data</returns>
        public long SendResponse() { return SendResponse(_response); }
        /// <summary>
        /// Send the HTTP response (synchronous)
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <returns>Size of sent data</returns>
        public long SendResponse(HttpResponse response) { return Send(response.Cache); }

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
        /// <param name="response">HTTP response</param>
        /// <returns>'true' if the current HTTP response was successfully sent, 'false' if the session is not connected</returns>
        public bool SendResponseAsync() { return SendResponseAsync(_response); }
        /// <summary>
        /// Send the HTTP response (asynchronous)
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <returns>'true' if the current HTTP response was successfully sent, 'false' if the session is not connected</returns>
        public bool SendResponseAsync(HttpResponse response) { return SendAsync(response.Cache); }

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
            if (_request.IsPendingHeader())
            {
                if (_request.ReceiveHeader(buffer, (int)offset, (int)size))
                    OnReceivedRequestHeader(_request);

                size = 0;
            }

            // Check for HTTP request error
            if (_request.IsErrorSet)
            {
                OnReceivedRequestError(_request, "Invalid HTTP request!");
                _request.Clear();
                Disconnect();
                return;
            }

            // Receive HTTP request body
            if (_request.ReceiveBody(buffer, (int)offset, (int)size))
            {
                OnReceivedRequestInternal(_request);
                _request.Clear();
                return;
            }

            // Check for HTTP request error
            if (_request.IsErrorSet)
            {
                OnReceivedRequestError(_request, "Invalid HTTP request!");
                _request.Clear();
                Disconnect();
                return;
            }
        }

        protected override void OnDisconnected()
        {
            // Receive HTTP request body
            if (_request.IsPendingBody())
            {
                OnReceivedRequestInternal(_request);
                _request.Clear();
                return;
            }
        }

        /// <summary>
        /// Handle HTTP request header received notification
        /// </summary>
        /// <remarks>Notification is called when HTTP request header was received from the client.</remarks>
        /// <param name="request">HTTP request</param>
        protected virtual void OnReceivedRequestHeader(HttpRequest request) { }

        /// <summary>
        /// Handle HTTP request received notification
        /// </summary>
        /// <remarks>Notification is called when HTTP request was received from the client.</remarks>
        /// <param name="request">HTTP request</param>
        protected virtual void OnReceivedRequest(HttpRequest request) { }

        /// <summary>
        /// Handle HTTP request error notification
        /// </summary>
        /// <remarks>Notification is called when HTTP request error was received from the client.</remarks>
        /// <param name="request">HTTP request</param>
        /// <param name="error">HTTP request error</param>
        protected virtual void OnReceivedRequestError(HttpRequest request, string error) { }

        #endregion

        /// <summary>
        /// HTTP request
        /// </summary>
        protected HttpRequest _request = new HttpRequest();
        /// <summary>
        /// HTTP request
        /// </summary>
        protected HttpResponse _response = new HttpResponse();

        // Static content cache
        internal FileCache cache = new FileCache();

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
