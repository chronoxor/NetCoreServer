using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;
using Xunit;

namespace tests
{
    class CommonCache
    {
        public static CommonCache GetInstance()
        {
            if (_instance == null)
                _instance = new CommonCache();
            return _instance;
        }

        public bool GetCache(string key, out string value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out value))
                    return true;
                else
                    return false;
            }
        }

        public void SetCache(string key, string value)
        {
            lock (_cacheLock)
                _cache[key] = value;
        }

        public bool DeleteCache(string key, out string value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out value))
                {
                    _cache.Remove(key);
                    return true;
                }
                else
                    return false;
            }
        }

        private readonly object _cacheLock = new object();
        private SortedDictionary<string, string> _cache = new SortedDictionary<string, string>();
        private static CommonCache _instance;
    }

    class HttpCacheSession : HttpSession
    {
        public HttpCacheSession(HttpServer server) : base(server) { }

        protected override void OnReceivedRequest(HttpRequest request)
        {
            // Process HTTP request methods
            if (request.Method == "HEAD")
                SendResponseAsync(Response.MakeHeadResponse());
            else if (request.Method == "GET")
            {
                // Get the cache value
                string cache;
                if (CommonCache.GetInstance().GetCache(request.Url, out cache))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(cache));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Required cache value was not found for the key: " + request.Url));
            }
            else if ((request.Method == "POST") || (request.Method == "PUT"))
            {
                // Set the cache value
                CommonCache.GetInstance().SetCache(request.Url, request.Body);
                // Response with the cache value
                SendResponseAsync(Response.MakeOkResponse());
            }
            else if (request.Method == "DELETE")
            {
                // Delete the cache value
                string cache;
                if (CommonCache.GetInstance().DeleteCache(request.Url, out cache))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(cache));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Deleted cache value was not found for the key: " + request.Url));
            }
            else if (request.Method == "OPTIONS")
                SendResponseAsync(Response.MakeOptionsResponse());
            else if (request.Method == "TRACE")
                SendResponseAsync(Response.MakeTraceResponse(request.Cache));
            else
                SendResponseAsync(Response.MakeErrorResponse("Unsupported HTTP method: " + request.Method));
        }

        protected override void OnReceivedRequestError(HttpRequest request, string error)
        {
            Console.WriteLine($"Request error: {error}");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTP session caught an error: {error}");
        }
    }

    class HttpCacheServer : HttpServer
    {
        public HttpCacheServer(IPAddress address, int port) : base(address, port) { }

        protected override TcpSession CreateSession() { return new HttpCacheSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTP session caught an error: {error}");
        }
    }

    public class HttpTests
    {
        [Fact(DisplayName = "HTTP server test")]
        public void HttpServerTest()
        {
            string address = "127.0.0.1";
            int port = 8080;

            // Create and start HTTP server
            var server = new HttpCacheServer(IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create a new HTTP client
            var client = new HttpClientEx(address, port);

            // Test CRUD operations
            var response = client.SendGetRequest("/test").Result;
            Assert.True(response.Status == 500);
            response = client.SendPostRequest("/test", "old_value").Result;
            Assert.True(response.Status == 200);
            response =  client.SendGetRequest("/test").Result;
            Assert.True(response.Status == 200);
            Assert.True(response.Body == "old_value");
            response = client.SendPutRequest("/test", "new_value").Result;
            Assert.True(response.Status == 200);
            response = client.SendGetRequest("/test").Result;
            Assert.True(response.Status == 200);
            Assert.True(response.Body == "new_value");
            response = client.SendDeleteRequest("/test").Result;
            Assert.True(response.Status == 200);
            response = client.SendGetRequest("/test").Result;
            Assert.True(response.Status == 500);

            // Stop the HTTP server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();
        }
    }
}
