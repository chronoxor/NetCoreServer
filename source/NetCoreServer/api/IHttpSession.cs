namespace NetCoreServer;

public interface IHttpSession : ISession
{
    /// <summary>
    /// Get the static content cache
    /// </summary>
    public FileCache Cache { get; }
    
    /// <summary>
    /// Get the HTTP request
    /// </summary>
    public HttpRequest Request { get; }   
    
    /// <summary>
    /// Get the HTTP response
    /// </summary>
    public HttpResponse Response { get; }

    /// <summary>
    /// Send the HTTP response (asynchronous)
    /// </summary>
    /// <param name="response">HTTP response</param>
    /// <returns>'true' if the current HTTP response was successfully sent, 'false' if the session is not connected</returns>

    bool SendResponseAsync(HttpResponse response);
}