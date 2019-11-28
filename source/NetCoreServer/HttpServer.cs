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
        public HttpServer(IPAddress address, int port) : base(address, port) {}
        /// <summary>
        /// Initialize HTTP server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public HttpServer(string address, int port) : base(address, port) {}
        /// <summary>
        /// Initialize HTTP server with a given IP endpoint
        /// </summary>
        /// <param name="endpoint">IP endpoint</param>
        public HttpServer(IPEndPoint endpoint) : base(endpoint) {}

        /// <summary>
        /// Get the static content cache
        /// </summary>
        public FileCache Cache { get { return cache; } }

        /// <summary>
        /// Add static content cache
        /// </summary>
        /// <param name="path">Static content path</param>
        /// <param name="prefix">Cache prefix (default is "/")</param>
        /// <param name="timeout">Refresh cache timeout (default is 1 hour)</param>
        public void AddStaticContent(string path, string prefix = "/", TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromHours(1);

            NetCoreServer.FileCache.InsertHandler handler = delegate (FileCache cache, string key, string value, TimeSpan timespan)
            {
                HttpResponse header = new HttpResponse();
                header.SetBegin(200);
                header.SetContentType(Path.GetExtension(key));
                header.SetHeader("Cache-Control", $"max-age={timespan.Seconds}");
                header.SetBody(value);
                return cache.Add(key, header.Cache, timespan);
            };

            Cache.InsertPath(path, prefix, timeout.Value, handler);
        }
        /// <summary>
        /// Remove static content cache
        /// </summary>
        /// <param name="path">Static content path</param>
        public void RemoveStaticContent(string path) { cache.RemovePath(path); }
        /// <summary>
        /// Clear static content cache
        /// </summary>
        public void ClearStaticContent() { cache.Clear(); }

        /// <summary>
        /// Watchdog the static content cache
        /// </summary>
        public void Watchdog(DateTime? utc = null) { utc ??= DateTime.UtcNow; cache.Watchdog(utc.Value); }

        protected override TcpSession CreateSession() { return new HttpSession(this); }

        // Static content cache
        internal FileCache cache = new FileCache();
    }

}
