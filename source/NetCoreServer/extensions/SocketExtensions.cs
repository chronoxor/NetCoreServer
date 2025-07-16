using System.Net.Sockets;

namespace NetCoreServer.extensions;

public static class SocketExtensions
{
    public static void SetupSocket(this Socket socket, int keepAliveTime, int keepAliveInterval,
        int keepAliveRetryCount)
    {
        // TODO implement for net standard 2.0
    }
}