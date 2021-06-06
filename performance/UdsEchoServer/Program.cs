using NDesk.Options;
using NetCoreServer;
using System;
using System.Net.Sockets;

namespace UdsEchoServer
{
    public class EchoSession : UdsSession
    {
        public EchoSession(UdsServer server) : base(server) { }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Resend the message back to the client
            SendAsync(buffer, offset, size);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Session caught an error with code {error}");
        }
    }

    public class EchoServer : UdsServer
    {
        public EchoServer(string patch) : base(patch) { }

        protected override UdsSession CreateSession() { return new EchoSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var help = false;
            var patch = @"C:\udsSock.sock";//need start with administrator rights

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "p|patch=", v => patch = v }
            };

            try
            {
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("Command line error: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `--help' to get usage information.");
                return;
            }

            if (help)
            {
                Console.WriteLine("Usage:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine($"Server patch: {patch}");

            Console.WriteLine();

            // Create a new echo server
            var server = new EchoServer(patch);

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (; ; )
            {
                var line = Console.ReadLine();
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