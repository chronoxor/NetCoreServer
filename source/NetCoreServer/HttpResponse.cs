using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NetCoreServer
{
    /// <summary>
    /// HTTP response is used to create or process parameters of HTTP protocol response(status, headers, etc).
    /// </summary>
    /// <remarks>Not thread-safe.</remarks>
    public class HttpResponse
    {
        static HttpResponse()
        {
            _mimeTable = new Dictionary<string, string>
            {
                // Base content types
                { ".html",      "text/html" },
                { ".css",       "text/css" },
                { ".js",        "text/javascript" },
                { ".vue",       "text/html" },
                { ".xml",       "text/xml" },

                // Application content types
                { ".atom",      "application/atom+xml" },
                { ".fastsoap",  "application/fastsoap" },
                { ".gzip",      "application/gzip" },
                { ".json",      "application/json" },
                { ".map",       "application/json" },
                { ".pdf",       "application/pdf" },
                { ".ps",        "application/postscript" },
                { ".soap",      "application/soap+xml" },
                { ".sql",       "application/sql" },
                { ".xslt",      "application/xslt+xml" },
                { ".zip",       "application/zip" },
                { ".zlib",      "application/zlib" },

                // Audio content types
                { ".aac",       "audio/aac" },
                { ".ac3",       "audio/ac3" },
                { ".mp3",       "audio/mpeg" },
                { ".ogg",       "audio/ogg" },

                // Font content types
                { ".ttf",       "font/ttf" },

                // Image content types
                { ".bmp",       "image/bmp" },
                { ".emf",       "image/emf" },
                { ".gif",       "image/gif" },
                { ".jpg",       "image/jpeg" },
                { ".jpm",       "image/jpm" },
                { ".jpx",       "image/jpx" },
                { ".jrx",       "image/jrx" },
                { ".png",       "image/png" },
                { ".svg",       "image/svg+xml" },
                { ".tiff",      "image/tiff" },
                { ".wmf",       "image/wmf" },

                // Message content types
                { ".http",      "message/http" },
                { ".s-http",    "message/s-http" },

                // Model content types
                { ".mesh",      "model/mesh" },
                { ".vrml",      "model/vrml" },

                // Text content types
                { ".csv",       "text/csv" },
                { ".plain",     "text/plain" },
                { ".richtext",  "text/richtext" },
                { ".rtf",       "text/rtf" },
                { ".rtx",       "text/rtx" },
                { ".sgml",      "text/sgml" },
                { ".strings",   "text/strings" },
                { ".url",       "text/uri-list" },

                // Video content types
                { ".H264",      "video/H264" },
                { ".H265",      "video/H265" },
                { ".mp4",       "video/mp4" },
                { ".mpeg",      "video/mpeg" },
                { ".raw",       "video/raw" }
            };
        }

        /// <summary>
        /// Initialize an empty HTTP response
        /// </summary>
        public HttpResponse()
        {
            Clear();
        }
        /// <summary>
        /// Initialize a new HTTP response with a given status and protocol
        /// </summary>
        /// <param name="status">HTTP status</param>
        /// <param name="protocol">Protocol version (default is "HTTP/1.1")</param>
        public HttpResponse(int status, string protocol = "HTTP/1.1")
        {
            SetBegin(status, protocol);
        }
        /// <summary>
        /// Initialize a new HTTP response with a given status, status phrase and protocol
        /// </summary>
        /// <param name="status">HTTP status</param>
        /// <param name="statusPhrase">HTTP status phrase</param>
        /// <param name="protocol">Protocol version</param>
        public HttpResponse(int status, string statusPhrase, string protocol)
        {
            SetBegin(status, statusPhrase, protocol);
        }

        /// <summary>
        /// Is the HTTP response empty?
        /// </summary>
        public bool IsEmpty { get { return (_cache.Size > 0); } }
        /// <summary>
        /// Is the HTTP response error flag set?
        /// </summary>
        public bool IsErrorSet { get; private set; }

        /// <summary>
        /// Get the HTTP response status
        /// </summary>
        public int Status { get; private set; }

        /// <summary>
        /// Get the HTTP response status phrase
        /// </summary>
        public string StatusPhrase { get { return _statusPhrase; } }
        /// <summary>
        /// Get the HTTP response protocol version
        /// </summary>
        public string Protocol { get { return _protocol; } }
        /// <summary>
        /// Get the HTTP response headers count
        /// </summary>
        public long Headers { get { return _headers.Count; } }
        /// <summary>
        /// Get the HTTP response header by index
        /// </summary>
        public (string, string) Header(int i)
        {
            Debug.Assert((i < _headers.Count), "Index out of bounds!");
            if (i >= _headers.Count)
                return ("", "");

            return _headers[i];
        }
        /// <summary>
        /// Get the HTTP response body as string
        /// </summary>
        public string Body { get { return _cache.ExtractString(_bodyIndex, _bodySize); } }
        /// <summary>
        /// Get the HTTP request body as byte array
        /// </summary>
        public byte[] BodyBytes { get { return _cache.Data[_bodyIndex..(_bodyIndex + _bodySize)]; } }
        /// <summary>
        /// Get the HTTP request body as read-only byte span
        /// </summary>
        public ReadOnlySpan<byte> BodySpan { get { return new ReadOnlySpan<byte>(_cache.Data, _bodyIndex, _bodySize); } }
        /// <summary>
        /// Get the HTTP response body length
        /// </summary>
        public long BodyLength { get { return _bodyLength; } }

        /// <summary>
        /// Get the HTTP response cache content
        /// </summary>
        public Buffer Cache { get { return _cache; } }

        /// <summary>
        /// Get string from the current HTTP response
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Status: {Status}");
            sb.AppendLine($"Status phrase: {StatusPhrase}");
            sb.AppendLine($"Protocol: {Protocol}");
            sb.AppendLine($"Headers: {Headers}");
            for (int i = 0; i < Headers; i++)
            {
                var header = Header(i);
                sb.AppendLine($"{header.Item1} : {header.Item2}");
            }
            sb.AppendLine($"Body: {BodyLength}");
            sb.AppendLine(Body);
            return sb.ToString();
        }

        /// <summary>
        /// Clear the HTTP response cache
        /// </summary>
        public HttpResponse Clear()
        {
            IsErrorSet = false;
            Status = 0;
            _statusPhrase = "";
            _protocol = "";
            _headers.Clear();
            _bodyIndex = 0;
            _bodySize = 0;
            _bodyLength = 0;
            _bodyLengthProvided = false;

            _cache.Clear();
            _cacheSize = 0;
            return this;
        }

        /// <summary>
        /// Set the HTTP response begin with a given status and protocol
        /// </summary>
        /// <param name="status">HTTP status</param>
        /// <param name="protocol">Protocol version (default is "HTTP/1.1")</param>
        public HttpResponse SetBegin(int status, string protocol = "HTTP/1.1")
        {
            string statusPhrase;

            switch (status)
            {
                case 100: statusPhrase = "Continue"; break;
                case 101: statusPhrase = "Switching Protocols"; break;
                case 102: statusPhrase = "Processing"; break;
                case 103: statusPhrase = "Early Hints"; break;

                case 200: statusPhrase = "OK"; break;
                case 201: statusPhrase = "Created"; break;
                case 202: statusPhrase = "Accepted"; break;
                case 203: statusPhrase = "Non-Authoritative Information"; break;
                case 204: statusPhrase = "No Content"; break;
                case 205: statusPhrase = "Reset Content"; break;
                case 206: statusPhrase = "Partial Content"; break;
                case 207: statusPhrase = "Multi-Status"; break;
                case 208: statusPhrase = "Already Reported"; break;

                case 226: statusPhrase = "IM Used"; break;

                case 300: statusPhrase = "Multiple Choices"; break;
                case 301: statusPhrase = "Moved Permanently"; break;
                case 302: statusPhrase = "Found"; break;
                case 303: statusPhrase = "See Other"; break;
                case 304: statusPhrase = "Not Modified"; break;
                case 305: statusPhrase = "Use Proxy"; break;
                case 306: statusPhrase = "Switch Proxy"; break;
                case 307: statusPhrase = "Temporary Redirect"; break;
                case 308: statusPhrase = "Permanent Redirect"; break;

                case 400: statusPhrase = "Bad Request"; break;
                case 401: statusPhrase = "Unauthorized"; break;
                case 402: statusPhrase = "Payment Required"; break;
                case 403: statusPhrase = "Forbidden"; break;
                case 404: statusPhrase = "Not Found"; break;
                case 405: statusPhrase = "Method Not Allowed"; break;
                case 406: statusPhrase = "Not Acceptable"; break;
                case 407: statusPhrase = "Proxy Authentication Required"; break;
                case 408: statusPhrase = "Request Timeout"; break;
                case 409: statusPhrase = "Conflict"; break;
                case 410: statusPhrase = "Gone"; break;
                case 411: statusPhrase = "Length Required"; break;
                case 412: statusPhrase = "Precondition Failed"; break;
                case 413: statusPhrase = "Payload Too Large"; break;
                case 414: statusPhrase = "URI Too Long"; break;
                case 415: statusPhrase = "Unsupported Media Type"; break;
                case 416: statusPhrase = "Range Not Satisfiable"; break;
                case 417: statusPhrase = "Expectation Failed"; break;

                case 421: statusPhrase = "Misdirected Request"; break;
                case 422: statusPhrase = "Unprocessable Entity"; break;
                case 423: statusPhrase = "Locked"; break;
                case 424: statusPhrase = "Failed Dependency"; break;
                case 425: statusPhrase = "Too Early"; break;
                case 426: statusPhrase = "Upgrade Required"; break;
                case 427: statusPhrase = "Unassigned"; break;
                case 428: statusPhrase = "Precondition Required"; break;
                case 429: statusPhrase = "Too Many Requests"; break;
                case 431: statusPhrase = "Request Header Fields Too Large"; break;

                case 451: statusPhrase = "Unavailable For Legal Reasons"; break;

                case 500: statusPhrase = "Internal Server Error"; break;
                case 501: statusPhrase = "Not Implemented"; break;
                case 502: statusPhrase = "Bad Gateway"; break;
                case 503: statusPhrase = "Service Unavailable"; break;
                case 504: statusPhrase = "Gateway Timeout"; break;
                case 505: statusPhrase = "HTTP Version Not Supported"; break;
                case 506: statusPhrase = "Variant Also Negotiates"; break;
                case 507: statusPhrase = "Insufficient Storage"; break;
                case 508: statusPhrase = "Loop Detected"; break;

                case 510: statusPhrase = "Not Extended"; break;
                case 511: statusPhrase = "Network Authentication Required"; break;

                default: statusPhrase = "Unknown"; break;
            }

            SetBegin(status, statusPhrase, protocol);
            return this;
        }

        /// <summary>
        /// Set the HTTP response begin with a given status, status phrase and protocol
        /// </summary>
        /// <param name="status">HTTP status</param>
        /// <param name="statusPhrase"> HTTP status phrase</param>
        /// <param name="protocol">Protocol version</param>
        public HttpResponse SetBegin(int status, string statusPhrase, string protocol)
        {
            // Clear the HTTP response cache
            Clear();

            // Append the HTTP response protocol version
            _cache.Append(protocol);
            _protocol = protocol;

            _cache.Append(" ");

            // Append the HTTP response status
            _cache.Append(status.ToString());
            Status = status;

            _cache.Append(" ");

            // Append the HTTP response status phrase
            _cache.Append(statusPhrase);
            _statusPhrase = statusPhrase;

            _cache.Append("\r\n");
            return this;
        }

        /// <summary>
        /// Set the HTTP response content type
        /// </summary>
        /// <param name="extension">Content extension</param>
        public HttpResponse SetContentType(string extension)
        {
            // Try to lookup the content type in mime table
            if (_mimeTable.TryGetValue(extension, out string mime))
                return SetHeader("Content-Type", mime);

            return this;
        }

        /// <summary>
        /// Set the HTTP response header
        /// </summary>
        /// <param name="key">Header key</param>
        /// <param name="value">Header value</param>
        public HttpResponse SetHeader(string key, string value)
        {
            // Append the HTTP response header's key
            _cache.Append(key);

            _cache.Append(": ");

            // Append the HTTP response header's value
            _cache.Append(value);

            _cache.Append("\r\n");

            // Add the header to the corresponding collection
            _headers.Add((key, value));
            return this;
        }

        /// <summary>
        /// Set the HTTP response cookie
        /// </summary>
        /// <param name="name">Cookie name</param>
        /// <param name="value">Cookie value</param>
        /// <param name="maxAge">Cookie age in seconds until it expires (default is 86400)</param>
        /// <param name="path">Cookie path (default is "")</param>
        /// <param name="domain">Cookie domain (default is "")</param>
        /// <param name="secure">Cookie secure flag (default is true)</param>
        /// <param name="strict">Cookie strict flag (default is true)</param>
        /// <param name="httpOnly">Cookie HTTP-only flag (default is true)</param>
        public HttpResponse SetCookie(string name, string value, int maxAge = 86400, string path = "", string domain = "", bool secure = true, bool strict = true, bool httpOnly = true)
        {
            string key = "Set-Cookie";

            // Append the HTTP response header's key
            _cache.Append(key);

            _cache.Append(": ");

            // Append the HTTP response header's value
            int valueIndex = (int)_cache.Size;

            // Append cookie
            _cache.Append(name);
            _cache.Append("=");
            _cache.Append(value);
            _cache.Append("; Max-Age=");
            _cache.Append(maxAge.ToString());
            if (!string.IsNullOrEmpty(domain))
            {
                _cache.Append("; Domain=");
                _cache.Append(domain);
            }
            if (!string.IsNullOrEmpty(path))
            {
                _cache.Append("; Path=");
                _cache.Append(path);
            }
            if (secure)
                _cache.Append("; Secure");
            if (strict)
                _cache.Append("; SameSite=Strict");
            if (httpOnly)
                _cache.Append("; HttpOnly");

            int valueSize = (int)_cache.Size - valueIndex;

            string cookie = _cache.ExtractString(valueIndex, valueSize);

            _cache.Append("\r\n");

            // Add the header to the corresponding collection
            _headers.Add((key, cookie));
            return this;
        }

        /// <summary>
        /// Set the HTTP response body
        /// </summary>
        /// <param name="body">Body string content (default is "")</param>
        public HttpResponse SetBody(string body = "") => SetBody(body.AsSpan());

        /// <summary>
        /// Set the HTTP response body
        /// </summary>
        /// <param name="body">Body string content as a span of characters</param>
        public HttpResponse SetBody(ReadOnlySpan<char> body)
        {
            int length = body.IsEmpty ? 0 : Encoding.UTF8.GetByteCount(body);

            // Append content length header
            SetHeader("Content-Length", length.ToString());

            _cache.Append("\r\n");

            int index = (int)_cache.Size;

            // Append the HTTP response body
            _cache.Append(body);
            _bodyIndex = index;
            _bodySize = length;
            _bodyLength = length;
            _bodyLengthProvided = true;
            return this;
        }

        /// <summary>
        /// Set the HTTP response body
        /// </summary>
        /// <param name="body">Body binary content</param>
        public HttpResponse SetBody(byte[] body) => SetBody(body.AsSpan());

        /// <summary>
        /// Set the HTTP response body
        /// </summary>
        /// <param name="body">Body binary content as a span of bytes</param>
        public HttpResponse SetBody(ReadOnlySpan<byte> body)
        {
            // Append content length header
            SetHeader("Content-Length", body.Length.ToString());

            _cache.Append("\r\n");

            int index = (int)_cache.Size;

            // Append the HTTP response body
            _cache.Append(body);
            _bodyIndex = index;
            _bodySize = body.Length;
            _bodyLength = body.Length;
            _bodyLengthProvided = true;
            return this;
        }

        /// <summary>
        /// Set the HTTP response body length
        /// </summary>
        /// <param name="length">Body length</param>
        public HttpResponse SetBodyLength(int length)
        {
            // Append content length header
            SetHeader("Content-Length", length.ToString());

            _cache.Append("\r\n");

            int index = (int)_cache.Size;

            // Clear the HTTP response body
            _bodyIndex = index;
            _bodySize = 0;
            _bodyLength = length;
            _bodyLengthProvided = true;
            return this;
        }

        /// <summary>
        /// Make OK response
        /// </summary>
        /// <param name="status">OK status (default is 200 (OK))</param>
        public HttpResponse MakeOkResponse(int status = 200)
        {
            Clear();
            SetBegin(status);
            SetBody();
            return this;
        }

        /// <summary>
        /// Make ERROR response
        /// </summary>
        /// <param name="content">Error content (default is "")</param>
        /// <param name="contentType">Error content type (default is "text/plain; charset=UTF-8")</param>
        public HttpResponse MakeErrorResponse(string content = "", string contentType = "text/plain; charset=UTF-8")
        {
            return MakeErrorResponse(500, content, contentType);
        }

        /// <summary>
        /// Make ERROR response
        /// </summary>
        /// <param name="status">Error status</param>
        /// <param name="content">Error content (default is "")</param>
        /// <param name="contentType">Error content type (default is "text/plain; charset=UTF-8")</param>
        public HttpResponse MakeErrorResponse(int status, string content = "", string contentType = "text/plain; charset=UTF-8")
        {
            Clear();
            SetBegin(status);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make HEAD response
        /// </summary>
        public HttpResponse MakeHeadResponse()
        {
            Clear();
            SetBegin(200);
            SetBody();
            return this;
        }

        /// <summary>
        /// Make GET response
        /// </summary>
        /// <param name="content">String content (default is "")</param>
        /// <param name="contentType">Content type (default is "text/plain; charset=UTF-8")</param>
        public HttpResponse MakeGetResponse(string content = "", string contentType = "text/plain; charset=UTF-8") => MakeGetResponse(content.AsSpan(), contentType);

        /// <summary>
        /// Make GET response
        /// </summary>
        /// <param name="content">String content as a span of characters</param>
        /// <param name="contentType">Content type (default is "text/plain; charset=UTF-8")</param>
        public HttpResponse MakeGetResponse(ReadOnlySpan<char> content, string contentType = "text/plain; charset=UTF-8")
        {
            Clear();
            SetBegin(200);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make GET response
        /// </summary>
        /// <param name="content">Binary content</param>
        /// <param name="contentType">Content type (default is "")</param>
        public HttpResponse MakeGetResponse(byte[] content, string contentType = "") => MakeGetResponse(content.AsSpan(), contentType);

        /// <summary>
        /// Make GET response
        /// </summary>
        /// <param name="content">Binary content as a span of bytes</param>
        /// <param name="contentType">Content type (default is "")</param>
        public HttpResponse MakeGetResponse(ReadOnlySpan<byte> content, string contentType = "")
        {
            Clear();
            SetBegin(200);
            if (!string.IsNullOrEmpty(contentType))
                SetHeader("Content-Type", contentType);
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make OPTIONS response
        /// </summary>
        /// <param name="allow">Allow methods (default is "HEAD,GET,POST,PUT,DELETE,OPTIONS,TRACE")</param>
        public HttpResponse MakeOptionsResponse(string allow = "HEAD,GET,POST,PUT,DELETE,OPTIONS,TRACE")
        {
            Clear();
            SetBegin(200);
            SetHeader("Allow", allow);
            SetBody();
            return this;
        }

        /// <summary>
        /// Make TRACE response
        /// </summary>
        /// <param name="content">String content</param>
        public HttpResponse MakeTraceResponse(string content) => MakeTraceResponse(content.AsSpan());

        /// <summary>
        /// Make TRACE response
        /// </summary>
        /// <param name="content">String content as a span of characters</param>
        public HttpResponse MakeTraceResponse(ReadOnlySpan<char> content)
        {
            Clear();
            SetBegin(200);
            SetHeader("Content-Type", "message/http");
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make TRACE response
        /// </summary>
        /// <param name="content">Binary content</param>
        public HttpResponse MakeTraceResponse(byte[] content) => MakeTraceResponse(content.AsSpan());

        /// <summary>
        /// Make TRACE response
        /// </summary>
        /// <param name="content">Binary content as a span of bytes</param>
        public HttpResponse MakeTraceResponse(ReadOnlySpan<byte> content)
        {
            Clear();
            SetBegin(200);
            SetHeader("Content-Type", "message/http");
            SetBody(content);
            return this;
        }

        /// <summary>
        /// Make TRACE response
        /// </summary>
        /// <param name="request">HTTP request</param>
        public HttpResponse MakeTraceResponse(HttpRequest request) => MakeTraceResponse(request.Cache.AsSpan());

        // HTTP response status phrase
        private string _statusPhrase;
        // HTTP response protocol
        private string _protocol;
        // HTTP response headers
        private List<(string, string)> _headers = new List<(string, string)>();
        // HTTP response body
        private int _bodyIndex;
        private int _bodySize;
        private int _bodyLength;
        private bool _bodyLengthProvided;

        // HTTP response cache
        private Buffer _cache = new Buffer();
        private int _cacheSize;

        // HTTP response mime table
        private static readonly Dictionary<string, string> _mimeTable;

        // Is pending parts of HTTP response
        internal bool IsPendingHeader()
        {
            return (!IsErrorSet && (_bodyIndex == 0));
        }
        internal bool IsPendingBody()
        {
            return (!IsErrorSet && (_bodyIndex > 0) && (_bodySize > 0));
        }

        // Receive parts of HTTP response
        internal bool ReceiveHeader(byte[] buffer, int offset, int size)
        {
            // Update the request cache
            _cache.Append(buffer, offset, size);

            // Try to seek for HTTP header separator
            for (int i = _cacheSize; i < (int)_cache.Size; i++)
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

                    // Parse protocol version
                    int protocolIndex = index;
                    int protocolSize = 0;
                    while (_cache[index] != ' ')
                    {
                        protocolSize++;
                        index++;
                        if (index >= (int)_cache.Size)
                            return false;
                    }
                    index++;
                    if ((index >= (int)_cache.Size))
                        return false;
                    _protocol = _cache.ExtractString(protocolIndex, protocolSize);

                    // Parse status code
                    int statusIndex = index;
                    int statusSize = 0;
                    while (_cache[index] != ' ')
                    {
                        if ((_cache[index] < '0') || (_cache[index] > '9'))
                            return false;
                        statusSize++;
                        index++;
                        if (index >= (int)_cache.Size)
                            return false;
                    }
                    Status = 0;
                    for (int j = statusIndex; j < (statusIndex + statusSize); j++)
                    {
                        Status *= 10;
                        Status += _cache[j] - '0';
                    }
                    index++;
                    if (index >= (int)_cache.Size)
                        return false;

                    // Parse status phrase
                    int statusPhraseIndex = index;
                    int statusPhraseSize = 0;
                    while (_cache[index] != '\r')
                    {
                        statusPhraseSize++;
                        index++;
                        if (index >= (int)_cache.Size)
                            return false;
                    }
                    index++;
                    if ((index >= (int)_cache.Size) || (_cache[index] != '\n'))
                        return false;
                    index++;
                    if (index >= (int)_cache.Size)
                        return false;
                    _statusPhrase = _cache.ExtractString(statusPhraseIndex, statusPhraseSize);

                    // Parse headers
                    while ((index < (int)_cache.Size) && (index < i))
                    {
                        // Parse header name
                        int headerNameIndex = index;
                        int headerNameSize = 0;
                        while (_cache[index] != ':')
                        {
                            headerNameSize++;
                            index++;
                            if (index >= i)
                                break;
                            if (index >= (int)_cache.Size)
                                return false;
                        }
                        index++;
                        if (index >= i)
                            break;
                        if (index >= (int)_cache.Size)
                            return false;

                        // Skip all prefix space characters
                        while (char.IsWhiteSpace((char)_cache[index]))
                        {
                            index++;
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
                            headerValueSize++;
                            index++;
                            if (index >= i)
                                break;
                            if (index >= (int)_cache.Size)
                                return false;
                        }
                        index++;
                        if ((index >= (int)_cache.Size) || (_cache[index] != '\n'))
                            return false;
                        index++;
                        if (index >= (int)_cache.Size)
                            return false;

                        // Validate header name and value (sometimes value can be empty)
                        if (headerNameSize == 0)
                            return false;

                        // Add a new header
                        string headerName = _cache.ExtractString(headerNameIndex, headerNameSize);
                        string headerValue = _cache.ExtractString(headerValueIndex, headerValueSize);
                        _headers.Add((headerName, headerValue));

                        // Try to find the body content length
                        if (string.Compare(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            _bodyLength = 0;
                            for (int j = headerValueIndex; j < (headerValueIndex + headerValueSize); j++)
                            {
                                if ((_cache[j] < '0') || (_cache[j] > '9'))
                                    return false;
                                _bodyLength *= 10;
                                _bodyLength += _cache[j] - '0';
                                _bodyLengthProvided = true;
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

            // Check if the body length was provided
            if (_bodyLengthProvided)
            {
                // Was the body fully received?
                if (_bodySize >= _bodyLength)
                {
                    _bodySize = _bodyLength;
                    return true;
                }
            }
            else
            {
                // Check the body content to find the response body end
                if (_bodySize >= 4)
                {
                    int index = _bodyIndex + _bodySize - 4;

                    // Was the body fully received?
                    if ((_cache[index + 0] == '\r') && (_cache[index + 1] == '\n') && (_cache[index + 2] == '\r') &&
                        (_cache[index + 3] == '\n'))
                    {
                        _bodyLength = _bodySize;
                        return true;
                    }
                }
            }

            // Body was received partially...
            return false;
        }
    }
}
