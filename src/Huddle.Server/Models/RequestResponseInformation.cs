using System.Net;

namespace Huddle.Server.Models;

/// <param name="ServiceProvider">The host app's service provider, for resolving services inside handlers.</param>
/// <param name="SourceHost">The connecting client's IP address, taken from the transport
/// (the remote endpoint of the HTTP connection), so it cannot be spoofed by headers.</param>
/// <param name="Request">The parsed body, query string and headers of the request.</param>
public record RequestContext(IServiceProvider ServiceProvider, string SourceHost, RequestInformation Request)
{
    /// <summary>
    /// Fires when the server is stopping, so long-running handlers (e.g. streamed uploads)
    /// can bail out. A client disconnecting mid-body does not fire this token - it surfaces
    /// as a <see cref="RequestAbortedException"/> from a <see cref="RequestInformation.BodyStream"/> read.
    /// </summary>
    public CancellationToken Aborted { get; init; }
}

/// <param name="Body">The request body materialized as a string. Null on routes mapped with
/// <see cref="HttpBodyMode.Streamed"/> - read <see cref="BodyStream"/> instead.</param>
/// <param name="QueryString">The parsed query string parameters.</param>
/// <param name="Headers">The request headers.</param>
public record RequestInformation(string? Body, Dictionary<string, string> QueryString, Dictionary<string, string> Headers)
{
    /// <summary>
    /// The raw request body on routes mapped with <see cref="HttpBodyMode.Streamed"/>
    /// (null otherwise). Read-once and forward-only, with no buffering behind it: the bytes
    /// come straight off the connection, so a multi-GB upload can be copied to a file with
    /// O(buffer) allocations. A read returns 0 when the body has ended cleanly and throws
    /// <see cref="RequestAbortedException"/> when the client disconnected mid-body.
    /// </summary>
    public Stream? BodyStream { get; init; }

    /// <summary>
    /// The request's declared Content-Length, or null when unknown (chunked transfer encoding).
    /// </summary>
    public long? ContentLength { get; init; }
}

public record ResponseInformation(HttpStatusCode StatusCode, string Response, string ResponseContentType);
