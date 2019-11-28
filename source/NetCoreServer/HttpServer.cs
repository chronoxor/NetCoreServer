using System;
using System.Net;
using System.IO;

namespace NetCoreServer
{
    /// <summary>
    /// HTTP server is used to create HTTP Web server and communicate with clients using HTTP protocol. It allows to receive GET, POST, PUT, DELETE requests and send HTTP responses.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    public class HttpServer : TcpServer
    {
        /// <summary>
        /// Initialize HTTP server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public HttpServer(IPAddress address, int port) : base(address, port) { Cache = new FileCache(); }
        /// <summary>
        /// Initialize HTTP server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public HttpServer(string address, int port) : base(address, port) { Cache = new FileCache(); }
        /// <summary>
        /// Initialize HTTP server with a given IP endpoint
        /// </summary>
        /// <param name="endpoint">IP endpoint</param>
        public HttpServer(IPEndPoint endpoint) : base(endpoint) { Cache = new FileCache(); }

        /// <summary>
        /// Get the static content cache
        /// </summary>
        public FileCache Cache { get; }

        /// <summary>
        /// Add static content cache
        /// </summary>
        /// <param name="path">Static content path</param>
        /// <param name="prefix">Cache prefix (default is "/")</param>
        /// <param name="timeout">Refresh cache timeout (default is 1 hour)</param>
        public void AddStaticContent(string path, string prefix = "/", TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromHours(1);

            bool Handler(FileCache cache, string key, byte[] value, TimeSpan timespan)
            {
                HttpResponse header = new HttpResponse();
                header.SetBegin(200);
                header.SetContentType(Path.GetExtension(key));
                header.SetHeader("Cache-Control", $"max-age={timespan.Seconds}");
                header.SetBody(value);
                return cache.Add(key, header.Cache.Data, timespan);
            }

            Cache.InsertPath(path, prefix, timeout.Value, Handler);
        }
        /// <summary>
        /// Remove static content cache
        /// </summary>
        /// <param name="path">Static content path</param>
        public void RemoveStaticContent(string path) { Cache.RemovePath(path); }
        /// <summary>
        /// Clear static content cache
        /// </summary>
        public void ClearStaticContent() { Cache.Clear(); }

        /// <summary>
        /// Watchdog the static content cache
        /// </summary>
        public void Watchdog(DateTime utc) { Cache.Watchdog(utc); }

        protected override TcpSession CreateSession() { return new HttpSession(this); }
    }
}
