// These types are part of Huddle.Server's public HTTP surface, so they use the
// Huddle.Server.Models namespace, but they are defined in Huddle.Core (which ships inside
// the Huddle.Server package) because the shared HTTP request pipeline that throws them
// lives here.
namespace Huddle.Server.Models;

/// <summary>
/// Thrown from a read on a streamed request body (RequestInformation.BodyStream) when the
/// client disconnected before sending the complete request body. A body that ends normally
/// is signalled by the read returning 0 instead, so handlers can distinguish "body ended"
/// from "client aborted" and clean up partial work (e.g. delete a half-written file).
/// </summary>
public sealed class RequestAbortedException : IOException
{
    public RequestAbortedException(string message)
        : base(message)
    {
    }

    public RequestAbortedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown from a read on a streamed request body (RequestInformation.BodyStream) when the
/// request body exceeds the configured maximum size. When it propagates out of a handler
/// the server responds with 413 Payload Too Large; bodies whose Content-Length already
/// exceeds the limit are rejected with 413 before the handler is invoked.
/// </summary>
public sealed class RequestBodyTooLargeException(long maxBodyBytes)
    : IOException($"The request body exceeds the configured maximum of {maxBodyBytes} bytes")
{
    public long MaxBodyBytes { get; } = maxBodyBytes;
}
