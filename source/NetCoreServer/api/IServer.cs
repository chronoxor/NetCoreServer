using System;
using System.Net;

namespace NetCoreServer;

public interface IServer : IDisposable
{
    /// <summary>
    /// Server Id
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Endpoint
    /// </summary>
    EndPoint Endpoint { get; }

    /// <summary>
    /// Number of sessions connected to the server
    /// </summary>
    long ConnectedSessions { get; }

    /// <summary>
    /// Number of bytes sent by the server
    /// </summary>
    long BytesSent { get; }

    /// <summary>
    /// Number of bytes received by the server
    /// </summary>
    long BytesReceived { get; }

    /// <summary>
    /// Is the server started?
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Start the server
    /// </summary>
    /// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
    bool Start();

    /// <summary>
    /// Stop the server
    /// </summary>
    /// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
    bool Stop();

    /// <summary>
    /// Restart the server
    /// </summary>
    /// <returns>'true' if the server was successfully restarted, 'false' if the server failed to restart</returns>
    bool Restart();
}