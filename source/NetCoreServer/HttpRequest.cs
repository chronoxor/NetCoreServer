using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NetCoreServer
{
    /// <summary>
    /// HTTP request is used to create or process parameters of HTTP protocol request(method, URL, headers, etc).
    /// </summary>
    /// <remarks>Not thread-safe.</remarks>
    public class HttpRequest
    {
        /// <summary>
        /// Initialize an empty HTTP request
        /// </summary>
        public HttpRequest()
        {
            Clear();
        }
        /// <summary>
        /// Initialize a new HTTP request with a given method, URL and protocol
        /// </summary>
        /// <param name="method">HTTP method</param>
        /// <param name="url">Requested URL</param>
        /// <param name="protocol">Protocol version (default is "HTTP/1.1")</param>
        public HttpRequest(string method, string url, string protocol = "HTTP/1.1")
        {
            SetBegin(method, url, protocol);
        }

        /// <summary>
        /// Is the HTTP request empty?
        /// </summary>
        public bool IsEmpty { get { return (_cache.Size == 0); } }
        /// <summary>
        /// Is the HTTP request error flag set?
        /// </summary>
        public bool IsErrorSet { get; private set; }

        /// <summary>
        /// Get the HTTP request method
        /// </summary>
        public string Method { get { return _method; } }
        /// <summary>
        /// Get the HTTP request URL
        /// </summary>
        public string Url { get { return _url; } }
        /// <summary>
        /// Get the HTTP request protocol version
        /// </summary>
        public string Protocol { get { return _protocol; } }
        /// <summary>
        /// Get the HTTP request headers count
        /// </summary>
        public long Headers { get { return _headers.Count; } }
        /// <summary>
        /// Get the HTTP request header by index
        /// </summary>
        public Tuple<string, string> Header(int i)
        {
            Debug.Assert((i < _headers.Count), "Index out of bounds!");
            if (i >= _headers.Count)
                return new Tuple<string, string>("", "");

            return _headers[i];
        }
        /// <summary>
        /// Get the HTTP request cookies count
        /// </summary>
        long Cookies { get { return _cookies.Count; } }
        /// <summary>
        /// Get the HTTP request cookie by index
        /// </summary>
        Tuple<string, string> Cookie(int i)
        {
            Debug.Assert((i < _cookies.Count), "Index out of bounds!");
            if (i >= _cookies.Count)
                return new Tuple<string, string>("", "");

            return _cookies[i];
        }
        /// <summary>
        /// Get the HTTP request body as string
        /// </summary>
        public string Body { get { return _cache.ExtractString(_bodyIndex, _bodySize); } }
        /// <summary>
        /// Get the HTTP request body length
        /// </summary>
        public long BodyLength { get { return _bodyLength; } }

        /// <summary>
        /// Get the HTTP request cache content
        /// </summary>
        public Buffer Cache { get { return _cache; } }

        /// <summary>
        /// Get string from the current HTTP request
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Request method: {Method}");
            sb.AppendLine($"Request URL: {Url}");
            sb.AppendLine($"Request protocol: {Protocol}");
            sb.AppendLine($"Request headers: {Headers}");
            for (int i = 0; i < Headers; ++i)
            {
                var header = Header(i);
                sb.AppendLine($"{header.Item1} : {header.Item2}");
            }
            sb.AppendLine($"Request body: {BodyLength}");
            sb.AppendLine(Body);
            return sb.ToString();
        }

        /// <summary>
        /// Clear the HTTP request cache
        /// </summary>
        public HttpRequest Clear()
        {
            IsErrorSet = false;
            _method = "";
            _url = "";
            _protocol = "";
            _headers.Clear();
            _cookies.Clear();
            _bodyIndex = 0;
            _bodySize = 0;
            _bodyLength = 0;
            _bodyLengthProvided = false;

            _cache.Clear();
            _cacheSize = 0;
            return this;
        }

        /// <summary>
        /// Set the HTTP request begin with a given method, URL and protocol
        /// </summary>
        /// <param name="method">HTTP method</param>
        /// <param name="url">Requested URL</param>
        /// <param name="protocol">Protocol version (default is "HTTP/1.1")</param>
        public HttpRequest SetBegin(string method, string url, string protocol = "HTTP/1.1")
        {
            // Clear the HTTP request cache
            Clear();

            // Append the HTTP request method
            _cache.Append(method);
            _method = method;

            _cache.Append(" ");

            // Append the HTTP request URL
            _cache.Append(url);
            _url = url;

            _cache.Append(" ");

            // Append the HTTP request protocol version
            _cache.Append(protocol);
            _protocol = protocol;

            _cache.Append("\r\n");
            return this;
        }

        /// <summary>
        /// Set the HTTP request header
        /// </summary>
        /// <param name="key">Header key</param>
        /// <param name="value">Header value</param>
        public HttpRequest SetHeader(string key, string value)
        {
            // Append the HTTP request header's key
            _cache.Append(key);

            _cache.Append(": ");

            // Append the HTTP request header's value
            _cache.Append(value);

            _cache.Append("\r\n");

            // Add the header to the corresponding collection
            _headers.Add(new Tuple<string, string>(key, value));
            return this;
        }

        /// <summary>
        /// Set the HTTP request cookie
        /// </summary>
        /// <param name="name">Cookie name</param>
        /// <param name="value">Cookie value</param>
        public HttpRequest SetCookie(string name, string value)
        {
            string key = "Cookie";
            string cookie = name + "=" + value;

            // Append the HTTP request header's key
            _cache.Append(key);

            _cache.Append(": ");

            // Append Cookie
            _cache.Append(cookie);

            _cache.Append("\r\n");

            // Add the header to the corresponding collection
            _headers.Add(new Tuple<string, string>(key, cookie));
            // Add the cookie to the corresponding collection
            _cookies.Add(new Tuple<string, string>(name, value));
            return this;
        }

        /// <summary>
        /// Add the HTTP request cookie
        /// </summary>
        /// <param name="name">Cookie name</param>
        /// <param name="value">Cookie value</param>
        public HttpRequest AddCookie(string name, string value)
        {
            // Append Cookie
            _cache.Append("; ");
            _cache.Append(name);
            _cache.Append("=");
            _cache.Append(value);

            // Add the cookie to the corresponding collection
            _cookies.Add(new Tuple<string, string>(name, value));
            return this;
        }

        /// <summary>
        /// Set the HTTP request body
        /// </summary>
        /// <param name="body">Body string content (default is "")</param>
        public HttpRequest SetBody(string body = "")
        {
            int length = string.IsNullOrEmpty(body) ? 0 : Encoding.UTF8.GetByteCount(body);

            // Append content length header
            SetHeader("Content-Length", length.ToString());

            _cache.Append("\r\n");

            int index = (int)_cache.Size;

            // Append the HTTP request body
            _cache.Append(body);
            _bodyIndex = index;
            _bodySize = length;
            _bodyLength = length;
            _bodyLengthProvided = true;
            return this;
        }

        /// <summary>
        /// Set the HTTP request body
        /// </summary>
        /// <param name="body">Body binary content</param>
        public HttpRequest SetBody(byte[] body)
        {
            // Append content length header
            SetHeader("Content-Length", body.Length.ToString());

            _cache.Append("\r\n");

            int index = (int)_cache.Size;

            // Append the HTTP request body
            _cache.Append(body);
            _bodyIndex = index;
            _bodySize = body.Length;
            _bodyLength = body.Length;
            _bodyLengthProvided = true;
            return this;
        }

        /// <summary>
        /// Set the HTTP request body
        /// </summary>
        /// <param name="body">Body buffer content</param>
        public HttpRequest SetBody(Buffer body)
        {
            // Append content length header
            SetHeader("Content-Length", body.Size.ToString());

            _cache.Append("\r\n");

            int index = (int)_cache.Size;

            // Append the HTTP request body
            _cache.Append(body.Data, body.Offset, body.Size);
            _bodyIndex = index;
            _bodySize = (int)body.Size;
            _bodyLength = (int)body.Size;
            _bodyLengthProvided = true;
            return this;
        }

        /// <summary>
        /// Set the HTTP request body length
        /// </summary>
        /// <param name="length">Body length</param>
        public HttpRequest SetBodyLength(int length)
        {
            // Append content length header
            SetHeader("Content-Length", length.ToString());

            _cache.Append("\r\n");

            int index = (int)_cache.Size;

            // Clear the HTTP request body
            _bodyIndex = index;
            _bodySize = 0;
            _bodyLength = length;
            _bodyLengthProvided = true;
            return this;
        }

        /// <summary>
        /// Make HEAD request
        /// </summary>
        /// <param name="url">URL to request</param>
        public HttpRequest MakeHeadRequest(string url)
        {
            Clear();
            SetBegin("HEAD", url);
            SetBody();
            return this;
        }

        /// <summary>
        /// Make GET request
        /// </summary>
        /// <param name="url">URL to request</param>
        public HttpRequest MakeGetRequest(string url)
        {
            Clear();
            SetBegin("GET", url);
            SetBody();
            return this;
        }

        /// <summary>
        /// Make POST request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="content">String content</param>
        /// <param name="contentType">Content type (default is "text/plain; charset=UTF-8")</param>
        public HttpRequest MakePostRequest(string url, string content, string contentType = "text/plain; charset=UTF-8")
        {
            Clear();
            SetBegin("POST", url);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make POST request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="content">Binary content</param>
        /// <param name="contentType">Content type (default is "")</param>
        public HttpRequest MakePostRequest(string url, byte[] content, string contentType = "")
        {
            Clear();
            SetBegin("POST", url);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make POST request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="content">Buffer content</param>
        /// <param name="contentType">Content type (default is "")</param>
        public HttpRequest MakePostRequest(string url, Buffer content, string contentType = "")
        {
            Clear();
            SetBegin("POST", url);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make PUT request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="content">String content</param>
        /// <param name="contentType">Content type (default is "text/plain; charset=UTF-8")</param> 
        public HttpRequest MakePutRequest(string url, string content, string contentType = "text/plain; charset=UTF-8")
        {
            Clear();
            SetBegin("PUT", url);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make PUT request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="content">Binary content</param>
        /// <param name="contentType">Content type (default is "")</param>
        public HttpRequest MakePutRequest(string url, byte[] content, string contentType = "")
        {
            Clear();
            SetBegin("PUT", url);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make PUT request
        /// </summary>
        /// <param name="url">URL to request</param>
        /// <param name="content">Buffer content</param>
        /// <param name="contentType">Content type (default is "")</param>
        public HttpRequest MakePutRequest(string url, Buffer content, string contentType = "")
        {
            Clear();
            SetBegin("PUT", url);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make DELETE request
        /// </summary>
        /// <param name="url">URL to request</param>
        public HttpRequest MakeDeleteRequest(string url)
        {
            Clear();
            SetBegin("DELETE", url);
            SetBody();
            return this;
        }

        /// <summary>
        /// Make OPTIONS request
        /// </summary>
        /// <param name="url">URL to request</param>
        public HttpRequest MakeOptionsRequest(string url)
        {
            Clear();
            SetBegin("OPTIONS", url);
            SetBody();
            return this;
        }

        /// <summary>
        /// Make TRACE request
        /// </summary>
        /// <param name="url">URL to request</param>
        public HttpRequest MakeTraceRequest(string url)
        {
            Clear();
            SetBegin("TRACE", url);
            SetBody();
            return this;
        }

        // HTTP request method
        private string _method;
        // HTTP request URL
        private string _url;
        // HTTP request protocol
        private string _protocol;
        // HTTP request headers
        private List<Tuple<string, string>> _headers = new List<Tuple<string, string>>();
        // HTTP request cookies
        private List<Tuple<string, string>> _cookies = new List<Tuple<string, string>>();
        // HTTP request body
        private int _bodyIndex;
        private int _bodySize;
        private int _bodyLength;
        private bool _bodyLengthProvided;

        // HTTP request cache
        private Buffer _cache = new Buffer();
        private int _cacheSize;

        // Is pending parts of HTTP request
        internal bool IsPendingHeader()
        {
            return (!IsErrorSet && (_bodyIndex == 0));
        }
        internal bool IsPendingBody()
        {
            return (!IsErrorSet && (_bodyIndex > 0) && (_bodySize > 0));
        }

        internal bool ReceiveHeader(byte[] buffer, int offset, int size)
        {
            // Update the request cache
            _cache.Append(buffer, offset, size);

            // Try to seek for HTTP header separator
            for (int i = _cacheSize; i < (int)_cache.Size; ++i)
            {
                // Check for the request cache out of bounds
                if ((i + 3) >= (int)_cache.Size)
                    break;

                // Check for the header separator
                if ((_cache[i + 0] == '\r') && (_cache[i + 1] == '\n') && (_cache[i + 2] == '\r') && (_cache[i + 3] == '\n'))
                {
                    int index = 0;

                    // Set the error flag for a while...
                    IsErrorSet = true;

                    // Parse method
                    int methodIndex = index;
                    int methodSize = 0;
                    while (_cache[index] != ' ')
                    {
                        ++methodSize;
                        ++index;
                        if (index >= (int)_cache.Size)
                            return false;
                    }
                    ++index;
                    if (index >= (int)_cache.Size)
                        return false;
                    _method = _cache.ExtractString(methodIndex, methodSize);

                    // Parse URL
                    int urlIndex = index;
                    int urlSize = 0;
                    while (_cache[index] != ' ')
                    {
                        ++urlSize;
                        ++index;
                        if (index >= (int)_cache.Size)
                            return false;
                    }
                    ++index;
                    if (index >= (int)_cache.Size)
                        return false;
                    _url = _cache.ExtractString(urlIndex, urlSize);

                    // Parse protocol version
                    int protocolIndex = index;
                    int protocolSize = 0;
                    while (_cache[index] != '\r')
                    {
                        ++protocolSize;
                        ++index;
                        if (index >= (int)_cache.Size)
                            return false;
                    }
                    ++index;
                    if ((index >= (int)_cache.Size) || (_cache[index] != '\n'))
                        return false;
                    ++index;
                    if (index >= (int)_cache.Size)
                        return false;
                    _protocol = _cache.ExtractString(protocolIndex, protocolSize);

                    // Parse headers
                    while ((index < (int)_cache.Size) && (index < i))
                    {
                        // Parse header name
                        int headerNameIndex = index;
                        int headerNameSize = 0;
                        while (_cache[index] != ':')
                        {
                            ++headerNameSize;
                            ++index;
                            if (index >= i)
                                break;
                            if (index >= (int)_cache.Size)
                                return false;
                        }
                        ++index;
                        if (index >= i)
                            break;
                        if (index >= (int)_cache.Size)
                            return false;

                        // Skip all prefix space characters
                        while (char.IsWhiteSpace((char)_cache[index]))
                        {
                            ++index;
                            if (index >= i)
                                break;
                            if (index >= (int)_cache.Size)
                                return false;
                        }

                        // Parse header value
                        int headerValueIndex = index;
                        int headerValueSize = 0;
                        while (_cache[index] != '\r')
                        {
                            ++headerValueSize;
                            ++index;
                            if (index >= i)
                                break;
                            if (index >= (int)_cache.Size)
                                return false;
                        }
                        ++index;
                        if ((index >= (int)_cache.Size) || (_cache[index] != '\n'))
                            return false;
                        ++index;
                        if (index >= (int)_cache.Size)
                            return false;

                        // Validate header name and value
                        if ((headerNameSize == 0) || (headerValueSize == 0))
                            return false;

                        // Add a new header
                        string headerName = _cache.ExtractString(headerNameIndex, headerNameSize);
                        string headerValue = _cache.ExtractString(headerValueIndex, headerValueSize);
                        _headers.Add(new Tuple<string, string>(headerName, headerValue));

                        // Try to find the body content length
                        if (headerName == "Content-Length")
                        {
                            _bodyLength = 0;
                            for (int j = headerValueIndex; j < (headerValueIndex + headerValueSize); ++j)
                            {
                                if ((_cache[j] < '0') || (_cache[j] > '9'))
                                    return false;
                                _bodyLength *= 10;
                                _bodyLength += _cache[j] - '0';
                                _bodyLengthProvided = true;
                            }
                        }

                        // Try to find Cookies
                        if (headerName == "Cookie")
                        {
                            bool name = true;
                            bool token = false;
                            int current = headerValueIndex;
                            int nameIndex = index;
                            int nameSize = 0;
                            int cookieIndex = index;
                            int cookieSize = 0;
                            for (int j = headerValueIndex; j < (headerValueIndex + headerValueSize); ++j)
                            {
                                if (_cache[j] == ' ')
                                {
                                    if (token)
                                    {
                                        if (name)
                                        {
                                            nameIndex = current;
                                            nameSize = j - current;
                                        }
                                        else
                                        {
                                            cookieIndex = current;
                                            cookieSize = j - current;
                                        }
                                    }
                                    token = false;
                                    continue;
                                }
                                if (_cache[j] == '=')
                                {
                                    if (token)
                                    {
                                        if (name)
                                        {
                                            nameIndex = current;
                                            nameSize = j - current;
                                        }
                                        else
                                        {
                                            cookieIndex = current;
                                            cookieSize = j - current;
                                        }
                                    }
                                    token = false;
                                    name = false;
                                    continue;
                                }
                                if (_cache[j] == ';')
                                {
                                    if (token)
                                    {
                                        if (name)
                                        {
                                            nameIndex = current;
                                            nameSize = j - current;
                                        }
                                        else
                                        {
                                            cookieIndex = current;
                                            cookieSize = j - current;
                                        }

                                        // Validate the cookie
                                        if ((nameSize > 0) && (cookieSize > 0))
                                        {
                                            // Add the cookie to the corresponding collection
                                            _cookies.Add(new Tuple<string, string>(_cache.ExtractString(nameIndex, nameSize), _cache.ExtractString(cookieIndex, cookieSize)));

                                            // Resset the current cookie values
                                            nameIndex = j;
                                            nameSize = 0;
                                            cookieIndex = j;
                                            cookieSize = 0;
                                        }
                                    }
                                    token = false;
                                    name = true;
                                    continue;
                                }
                                if (!token)
                                {
                                    current = j;
                                    token = true;
                                }
                            }

                            // Process the last cookie
                            if (token)
                            {
                                if (name)
                                {
                                    nameIndex = current;
                                    nameSize = headerValueIndex + headerValueSize - current;
                                }
                                else
                                {
                                    cookieIndex = current;
                                    cookieSize = headerValueIndex + headerValueSize - current;
                                }

                                // Validate the cookie
                                if ((nameSize > 0) && (cookieSize > 0))
                                {
                                    // Add the cookie to the corresponding collection
                                    _cookies.Add(new Tuple<string, string>(_cache.ExtractString(nameIndex, nameSize), _cache.ExtractString(cookieIndex, cookieSize)));
                                }
                            }
                        }
                    }

                    // Reset the error flag
                    IsErrorSet = false;

                    // Update the body index and size
                    _bodyIndex = i + 4;
                    _bodySize = (int)_cache.Size - i - 4;

                    // Update the parsed cache size
                    _cacheSize = (int)_cache.Size;

                    return true;
                }
            }

            // Update the parsed cache size
            _cacheSize = ((int)_cache.Size >= 3) ? ((int)_cache.Size - 3) : 0;

            return false;
        }

        internal bool ReceiveBody(byte[] buffer, int offset, int size)
        {
            // Update the request cache
            _cache.Append(buffer, offset, size);

            // Update the parsed cache size
            _cacheSize = (int)_cache.Size;

            // Update body size
            _bodySize += size;

            // GET request has no body
            if ((Method == "HEAD") || (Method == "GET") || (Method == "OPTIONS") || (Method == "TRACE"))
            {
                _bodyLength = 0;
                _bodySize = 0;
                return true;
            }

            // Check if the body was fully parsed
            if (_bodyLengthProvided && (_bodySize >= _bodyLength))
            {
                _bodySize = _bodyLength;
                return true;
            }

            return false;
        }
    }
}
