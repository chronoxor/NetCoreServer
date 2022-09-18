using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NetCoreServer;

namespace HttpsServer
{
    class CommonCache
    {
        public static CommonCache GetInstance()
        {
            if (_instance == null)
                _instance = new CommonCache();
            return _instance;
        }

        public string GetAllCache()
        {
            var result = new StringBuilder();
            result.Append("[\n");
            foreach (var item in _cache)
            {
                result.Append("  {\n");
                result.AppendFormat($"    \"key\": \"{item.Key}\",\n");
                result.AppendFormat($"    \"value\": \"{item.Value}\",\n");
                result.Append("  },\n");
            }
            result.Append("]\n");
            return result.ToString();
        }

        public bool GetCacheValue(string key, out string value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void PutCacheValue(string key, string value)
        {
            _cache[key] = value;
        }

        public bool DeleteCacheValue(string key, out string value)
        {
            return _cache.TryRemove(key, out value);
        }

        private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();
        private static CommonCache _instance;
    }

    class HttpsCacheSession : HttpsSession
    {
        public HttpsCacheSession(NetCoreServer.HttpsServer server) : base(server) {}

        protected override void OnReceivedRequest(HttpRequest request)
        {
            // Show HTTP request content
            Console.WriteLine(request);

            // Process HTTP request methods
            if (request.Method == "HEAD")
                SendResponseAsync(Response.MakeHeadResponse());
            else if (request.Method == "GET")
            {
                string key = request.Url;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                if (string.IsNullOrEmpty(key))
                {
                    // Response with all cache values
                    SendResponseAsync(Response.MakeGetResponse(CommonCache.GetInstance().GetAllCache(), "application/json; charset=UTF-8"));
                }
                // Get the cache value by the given key
                else if (CommonCache.GetInstance().GetCacheValue(key, out var value))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(value));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse(404, "Required cache value was not found for the key: " + key));
            }
            else if ((request.Method == "POST") || (request.Method == "PUT"))
            {
                string key = request.Url;
                string value = request.Body;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                // Put the cache value
                CommonCache.GetInstance().PutCacheValue(key, value);

                // Response with the cache value
                SendResponseAsync(Response.MakeOkResponse());
            }
            else if (request.Method == "DELETE")
            {
                string key = request.Url;

                // Decode the key value
                key = Uri.UnescapeDataString(key);
                key = key.Replace("/api/cache", "", StringComparison.InvariantCultureIgnoreCase);
                key = key.Replace("?key=", "", StringComparison.InvariantCultureIgnoreCase);

                // Delete the cache value
                if (CommonCache.GetInstance().DeleteCacheValue(key, out var value))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(value));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse(404, "Deleted cache value was not found for the key: " + key));
            }
            else if (request.Method == "OPTIONS")
                SendResponseAsync(Response.MakeOptionsResponse());
            else if (request.Method == "TRACE")
                SendResponseAsync(Response.MakeTraceResponse(request));
            else
                SendResponseAsync(Response.MakeErrorResponse("Unsupported HTTP method: " + request.Method));
        }

        protected override void OnReceivedRequestError(HttpRequest request, string error)
        {
            Console.WriteLine($"Request error: {error}");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTPS session caught an error: {error}");
        }
    }

    class HttpsCacheServer : NetCoreServer.HttpsServer
    {
        public HttpsCacheServer(SslContext context, IPAddress address, int port) : base(context, address, port) {}

        protected override SslSession CreateSession() { return new HttpsCacheSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTPS server caught an error: {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // HTTPS server port
            int port = 8443;
            if (args.Length > 0)
                port = int.Parse(args[0]);
            // HTTPS server content path
            string www = "../../../../../www/api";
            if (args.Length > 1)
                www = args[1];

            Console.WriteLine($"HTTPS server port: {port}");
            Console.WriteLine($"HTTPS server static content path: {www}");
            Console.WriteLine($"HTTPS server website: https://localhost:{port}/api/index.html");

            Console.WriteLine();

            // Create and prepare a new SSL server context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("server.pfx", "qwerty"));

            // Create a new HTTP server
            var server = new HttpsCacheServer(context, IPAddress.Any, port);
            server.AddStaticContent(www, "/api");

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Restart the server
                if (line == "!")
                {
                    Console.Write("Server restarting...");
                    server.Restart();
                    Console.WriteLine("Done!");
                }
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
