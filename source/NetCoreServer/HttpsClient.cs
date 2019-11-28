using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetCoreServer
{
    /// <summary>
    /// HTTPS client is used to communicate with secured HTTPS Web server. It allows to send GET, POST, PUT, DELETE requests and receive HTTP result using secure transport.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    public class HttpsClient : SslClient
    {
        /// <summary>
        /// Initialize HTTPS client with a given IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public HttpsClient(SslContext context, IPAddress address, int port) : base(context, address, port) { }
        /// <summary>
        /// Initialize HTTPS client with a given IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public HttpsClient(SslContext context, string address, int port) : base(context, address, port) { }
        /// <summary>
        /// Initialize HTTPS client with a given IP endpoint
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="endpoint">IP endpoint</param>
        public HttpsClient(SslContext context, IPEndPoint endpoint) : base(context, endpoint) { }

        /// <summary>
        /// Get the HTTP request
        /// </summary>
        public HttpRequest Request { get { return _request; } }

        #region Send request / Send request body

        /// <summary>
        /// Send the current HTTP request (synchronous)
        /// </summary>
        /// <returns>Size of sent data</returns>
        public long SendRequest() { return SendRequest(_request); }
        /// <summary>
        /// Send the HTTP request (synchronous)
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <returns>Size of sent data</returns>
        public long SendRequest(HttpRequest request) { return Send(request.Cache); }

        /// <summary>
        /// Send the HTTP request body (synchronous)
        /// </summary>
        /// <param name="body">HTTP request body</param>
        /// <returns>Size of sent data</returns>
        public long SendRequestBody(string body) { return Send(body); }
        /// <summary>
        /// Send the HTTP request body (synchronous)
        /// </summary>
        /// <param name="buffer">HTTP request body buffer</param>
        /// <returns>Size of sent data</returns>
        public long SendRequestBody(byte[] buffer) { return Send(buffer); }
        /// <summary>
        /// Send the HTTP request body (synchronous)
        /// </summary>
        /// <param name="buffer">HTTP request body buffer</param>
        /// <param name="offset">HTTP request body buffer offset</param>
        /// <param name="size">HTTP request body size</param>
        /// <returns>Size of sent data</returns>
        public long SendRequestBody(byte[] buffer, long offset, long size) { return Send(buffer, offset, size); }

        /// <summary>
        /// Send the current HTTP request (asynchronous)
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <returns>'true' if the current HTTP request was successfully sent, 'false' if the session is not connected</returns>
        public bool SendRequestAsync() { return SendRequestAsync(_request); }
        /// <summary>
        /// Send the HTTP request (asynchronous)
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <returns>'true' if the current HTTP request was successfully sent, 'false' if the session is not connected</returns>
        public bool SendRequestAsync(HttpRequest request) { return SendAsync(request.Cache); }

        /// <summary>
        /// Send the HTTP request body (asynchronous)
        /// </summary>
        /// <param name="body">HTTP request body</param>
        /// <returns>'true' if the HTTP request body was successfully sent, 'false' if the session is not connected</returns>
        public bool SendRequestBodyAsync(string body) { return SendAsync(body); }
        /// <summary>
        /// Send the HTTP request body (asynchronous)
        /// </summary>
        /// <param name="buffer">HTTP request body buffer</param>
        /// <returns>'true' if the HTTP request body was successfully sent, 'false' if the session is not connected</returns>
        public bool SendRequestBodyAsync(byte[] buffer) { return SendAsync(buffer); }
        /// <summary>
        /// Send the HTTP request body (asynchronous)
        /// </summary>
        /// <param name="buffer">HTTP request body buffer</param>
        /// <param name="offset">HTTP request body buffer offset</param>
        /// <param name="size">HTTP request body size</param>
        /// <returns>'true' if the HTTP request body was successfully sent, 'false' if the session is not connected</returns>
        public bool SendRequestBodyAsync(byte[] buffer, long offset, long size) { return SendAsync(buffer, offset, size); }

        #endregion

        #region Session handlers

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Receive HTTP response header
            if (_response.IsPendingHeader())
            {
                if (_response.ReceiveHeader(buffer, (int)offset, (int)size))
                    OnReceivedResponseHeader(_response);

                size = 0;
            }

            // Check for HTTP response error
            if (_response.IsErrorSet)
            {
                OnReceivedResponseError(_response, "Invalid HTTP response!");
                _response.Clear();
                Disconnect();
                return;
            }

            // Receive HTTP response body
            if (_response.ReceiveBody(buffer, (int)offset, (int)size))
            {
                OnReceivedResponse(_response);
                _response.Clear();
                return;
            }

            // Check for HTTP response error
            if (_response.IsErrorSet)
            {
                OnReceivedResponseError(_response, "Invalid HTTP response!");
                _response.Clear();
                Disconnect();
                return;
            }
        }

        protected override void OnDisconnected()
        {
            // Receive HTTP response body
            if (_response.IsPendingBody())
            {
                OnReceivedResponse(_response);
                _response.Clear();
                return;
            }
        }

        /// <summary>
        /// Handle HTTP response header received notification
        /// </summary>
        /// <remarks>Notification is called when HTTP response header was received from the server.</remarks>
        /// <param name="request">HTTP request</param>
        protected virtual void OnReceivedResponseHeader(HttpResponse response) { }

        /// <summary>
        /// Handle HTTP response received notification
        /// </summary>
        /// <remarks>Notification is called when HTTP response was received from the server.</remarks>
        /// <param name="request">HTTP response</param>
        protected virtual void OnReceivedResponse(HttpResponse response) { }

        /// <summary>
        /// Handle HTTP response error notification
        /// </summary>
        /// <remarks>Notification is called when HTTP response error was received from the server.</remarks>
        /// <param name="request">HTTP response</param>
        /// <param name="error">HTTP response error</param>
        protected virtual void OnReceivedResponseError(HttpResponse response, string error) { }

        #endregion

        /// <summary>
        /// HTTP request
        /// </summary>
        protected HttpRequest _request = new HttpRequest();
        /// <summary>
        /// HTTP response
        /// </summary>
        protected HttpResponse _response = new HttpResponse();
    }

    /// <summary>
    /// HTTPS extended client make requests to HTTPS Web server with returning Task as a synchronization primitive.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    public class HttpsClientEx : HttpsClient
    {
        /// <summary>
        /// Initialize HTTPS client with a given IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public HttpsClientEx(SslContext context, IPAddress address, int port) : base(context, address, port) { }
        /// <summary>
        /// Initialize HTTPS client with a given IP address and port number
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public HttpsClientEx(SslContext context, string address, int port) : base(context, address, port) { }
        /// <summary>
        /// Initialize HTTPS client with a given IP endpoint
        /// </summary>
        /// <param name="context">SSL context</param>
        /// <param name="endpoint">IP endpoint</param>
        public HttpsClientEx(SslContext context, IPEndPoint endpoint) : base(context, endpoint) { }

        #region Send request

        /// <summary>
        /// Send current HTTP request
        /// </summary>
        /// <param name="timeout">Current HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendRequest(TimeSpan? timeout = null) { return SendRequest(_request, timeout); }
        /// <summary>
        /// Send HTTP request
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="timeout">HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendRequest(HttpRequest request, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMinutes(1);

            _tcs = new TaskCompletionSource<HttpResponse>();
            _request = request;

            // Check if the HTTP request is valid
            if (_request.IsEmpty || _request.IsErrorSet)
            {
                SetTCSError("Invalid HTTP request!");
                return _tcs.Task;
            }

            if (!IsHandshaked)
            {
                // Connect to the Web server
                if (!ConnectAsync())
                {
                    SetTCSError("Connection failed!");
                    return _tcs.Task;
                }
            }
            else
            {
                // Send prepared HTTP request
                if (!SendRequestAsync())
                {
                    SetTCSError("Failed to send HTTP request!");
                    return _tcs.Task;
                }
            }

            TimerCallback timeoutHandler = delegate (object canceled)
            {
                if ((bool)canceled)
                    return;

                // Disconnect on timeout
                OnReceivedResponseError(_response, "Timeout!");
                _response.Clear();
                DisconnectAsync();
            };

            if (_timer == null)
                _timer = new Timer(timeoutHandler, false, (int)timeout.Value.TotalMilliseconds, Timeout.Infinite);
            else
                _timer.Change((int)timeout.Value.TotalMilliseconds, Timeout.Infinite);

            return _tcs.Task;
        }

        /// <summary>
        /// Send HEAD request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="timeout">Current HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendHeadRequest(string url, TimeSpan? timeout = null) { return SendRequest(_request.MakeHeadRequest(url), timeout); }
        /// <summary>
        /// Send GET request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="timeout">Current HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendGetRequest(string url, TimeSpan? timeout = null) { return SendRequest(_request.MakeGetRequest(url), timeout); }
        /// <summary>
        /// Send POST request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="content">Content</param>
        /// <param name="timeout">Current HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendPostRequest(string url, string content, TimeSpan? timeout = null) { return SendRequest(_request.MakePostRequest(url, content), timeout); }
        /// <summary>
        /// Send PUT request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="content">Content</param>
        /// <param name="timeout">Current HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendPutRequest(string url, string content, TimeSpan? timeout = null) { return SendRequest(_request.MakePutRequest(url, content), timeout); }
        /// <summary>
        /// Send DELETE request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="timeout">Current HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendDeleteRequest(string url, TimeSpan? timeout = null) { return SendRequest(_request.MakeDeleteRequest(url), timeout); }
        /// <summary>
        /// Send OPTIONS request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="timeout">Current HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendOptionsRequest(string url, TimeSpan? timeout = null) { return SendRequest(_request.MakeOptionsRequest(url), timeout); }
        /// <summary>
        /// Send TRACE request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="timeout">Current HTTP request timeout (default is 1 minute)</param>
        /// <returns>HTTP request Task</returns>
        public Task<HttpResponse> SendTraceRequest(string url, TimeSpan? timeout = null) { return SendRequest(_request.MakeTraceRequest(url), timeout); }

        #endregion

        #region Session handlers

        protected override void OnHandshaked()
        {
            // Send prepared HTTP request on connect
            if (!_request.IsEmpty && !_request.IsErrorSet)
                if (!SendRequestAsync())
                    SetTCSError("Failed to send HTTP request!");
        }

        protected override void OnDisconnected()
        {
            // Cancel timeout check timer
            if (_timer != null)
                _timer.Dispose();
            _timer = null;

            base.OnDisconnected();
        }

        protected override void OnReceivedResponse(HttpResponse response)
        {
            // Cancel timeout check timer
            _timer.Dispose();
            _timer = null;

            SetTCSValue(response);
        }

        protected override void OnReceivedResponseError(HttpResponse response, string error)
        {
            // Cancel timeout check timer
            _timer.Dispose();
            _timer = null;

            SetTCSError(error);
        }

        #endregion

        private TaskCompletionSource<HttpResponse> _tcs = new TaskCompletionSource<HttpResponse>();
        private Timer _timer;

        private void SetTCSValue(HttpResponse response)
        {
            var newResponse = new HttpResponse();
            newResponse.Swap(response);
            _tcs.SetResult(newResponse);
            _request.Clear();
        }

        private void SetTCSError(string error)
        {
            _tcs.SetException(new Exception(error));
            _request.Clear();
        }
    }
}
