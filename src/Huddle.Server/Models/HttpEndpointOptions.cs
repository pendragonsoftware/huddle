namespace Huddle.Server.Models;

public enum HttpBodyMode
{
    /// <summary>
    /// The whole request body is read into <see cref="RequestInformation.Body"/> as a string
    /// before the handler runs. This is the default, matching pre-0.10 behaviour.
    /// </summary>
    Buffered,

    /// <summary>
    /// The handler receives the raw body via <see cref="RequestInformation.BodyStream"/>
    /// without any buffering, for large binary payloads. <see cref="RequestInformation.Body"/>
    /// is null on these routes.
    /// </summary>
    Streamed
}

/// <summary>
/// Per-route options for <see cref="Builders.IHttpApiBuilder"/> map overloads.
/// </summary>
public sealed class HttpEndpointOptions
{
    public HttpBodyMode BodyMode { get; init; } = HttpBodyMode.Buffered;

    /// <summary>
    /// Maximum allowed request body size in bytes. Requests declaring a larger Content-Length
    /// are rejected with 413 before the handler runs; chunked bodies are cut off with 413 as
    /// soon as they exceed the limit. Null (the default) applies the server-wide value from
    /// <see cref="Builders.IHttpApiBuilder.WithMaxRequestBodyBytes"/>, if any.
    /// </summary>
    public long? MaxBodyBytes { get; init; }
}
