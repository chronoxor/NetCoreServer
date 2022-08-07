using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using NetCoreServer;
using Xunit;

namespace tests
{
    class HttpsCacheSession : HttpsSession
    {
        public HttpsCacheSession(HttpsServer server) : base(server) {}

        protected override void OnReceivedRequest(HttpRequest request)
        {
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

    class HttpsCacheServer : HttpsServer
    {
        public HttpsCacheServer(SslContext context, IPAddress address, int port) : base(context, address, port) {}

        protected override SslSession CreateSession() { return new HttpsCacheSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTPS server caught an error: {error}");
        }
    }

    public class HttpsTests
    {
        [Fact(DisplayName = "HTTPS server test")]
        public void HttpsServerTest()
        {
            string address = "127.0.0.1";
            int port = 8443;

            // Create and prepare a new SSL server and client context
            var server_context = new SslContext(SslProtocols.Tls12, new X509Certificate2("server.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);
            var client_context = new SslContext(SslProtocols.Tls12, new X509Certificate2("client.pfx", "qwerty"), (sender, certificate, chain, sslPolicyErrors) => true);

            // Create and start HTTPS server
            var server = new HttpsCacheServer(server_context, IPAddress.Any, port);
            Assert.True(server.Start());
            while (!server.IsStarted)
                Thread.Yield();

            // Create a new HTTPS client
            var client = new HttpsClientEx(client_context, address, port);

            // Test CRUD operations
            var response = client.SendGetRequest("/test").Result;
            Assert.True(response.Status == 404);
            response = client.SendPostRequest("/test", "old_value").Result;
            Assert.True(response.Status == 200);
            response = client.SendGetRequest("/test").Result;
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
            Assert.True(response.Status == 404);

            // Stop the HTTPS server
            Assert.True(server.Stop());
            while (server.IsStarted)
                Thread.Yield();
        }
    }
}
