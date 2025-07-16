using System;
using System.Net.Sockets;

namespace NetCoreServer;

public interface ISession : IDisposable
{
    /// <summary>
    /// Session Id
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Server
    /// </summary>
    IServer GetServer();

    /// <summary>
    /// Socket
    /// </summary>
    Socket Socket { get; }

    /// <summary>
    /// Number of bytes pending sent by the session
    /// </summary>
    public long BytesPending { get; }

    /// <summary>
    /// Number of bytes sending by the session
    /// </summary>
    public long BytesSending { get; }

    /// <summary>
    /// Number of bytes sent by the session
    /// </summary>
    public long BytesSent { get; }

    /// <summary>
    /// Number of bytes received by the session
    /// </summary>
    public long BytesReceived { get; }

    /// <summary>
    /// Is the session connected?
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Disconnect the session
    /// </summary>
    /// <returns>'true' if the section was successfully disconnected, 'false' if the section is already disconnected</returns>
    bool Disconnect();

    /// <summary>
    /// Send data to the client (asynchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
    bool SendAsync(byte[] buffer);

    /// <summary>
    /// Send data to the client (asynchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send as a span of bytes</param>
    /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
    bool SendAsync(ReadOnlySpan<byte> buffer);

    /// <summary>
    /// Send text to the client (asynchronous)
    /// </summary>
    /// <param name="text">Text to send as a span of characters</param>
    /// <returns>'true' if the text was successfully sent, 'false' if the session is not connected</returns>
    bool SendAsync(ReadOnlySpan<char> text);

    /// <summary>
    /// Receive data from the client (asynchronous)
    /// </summary>
    void ReceiveAsync();
}