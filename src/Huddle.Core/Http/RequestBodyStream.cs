using Huddle.Server.Models;
using System.Net;
using System.Net.Sockets;

namespace Huddle.Core.Http;

/// <summary>
/// Read-once, forward-only wrapper over the raw request body stream. It never buffers the
/// payload - each read passes straight through to the transport - and adds three behaviours:
/// counts bytes to enforce an optional maximum body size, detects truncated bodies
/// (fewer bytes than Content-Length promised) and turns transport-level disconnect errors
/// into <see cref="RequestAbortedException"/> so handlers can tell "body ended" (a read
/// returns 0) apart from "client aborted" (a read throws).
/// </summary>
internal sealed class RequestBodyStream(Stream inner, long? contentLength, long? maxBodyBytes) : Stream
{
    private long _bytesRead;
    private bool _reachedEnd;

    /// <summary>Total bytes handed to the caller so far.</summary>
    public long BytesRead => _bytesRead;

    /// <summary>True once the body has been fully and cleanly consumed.</summary>
    public bool ReachedEnd => _reachedEnd;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    /// <summary>The request's Content-Length. Throws when the length is unknown (chunked bodies).</summary>
    public override long Length => contentLength ?? throw new NotSupportedException("The request did not declare a Content-Length");

    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int read;
        try
        {
            read = inner.Read(buffer);
        }
        catch (Exception ex) when (IsDisconnect(ex))
        {
            throw Aborted(ex);
        }

        return Account(read);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read;
        try
        {
            read = await inner.ReadAsync(buffer, cancellationToken);
        }
        catch (Exception ex) when (IsDisconnect(ex))
        {
            throw Aborted(ex);
        }

        return Account(read);
    }

    private int Account(int read)
    {
        if (read == 0)
        {
            // With a declared Content-Length, running out of bytes early means the client
            // went away mid-body - without one (chunked), end-of-stream is the clean end.
            if (contentLength.HasValue && _bytesRead < contentLength.Value)
            {
                throw new RequestAbortedException(
                    $"The client disconnected after sending {_bytesRead} of {contentLength.Value} bytes");
            }

            _reachedEnd = true;
            return 0;
        }

        _bytesRead += read;

        if (maxBodyBytes.HasValue && _bytesRead > maxBodyBytes.Value)
        {
            throw new RequestBodyTooLargeException(maxBodyBytes.Value);
        }

        if (contentLength.HasValue && _bytesRead >= contentLength.Value)
        {
            _reachedEnd = true;
        }

        return read;
    }

    private RequestAbortedException Aborted(Exception ex) =>
        new("The client disconnected while the request body was being read", ex);

    private static bool IsDisconnect(Exception ex) =>
        ex is HttpListenerException or SocketException or ObjectDisposedException
        || (ex is IOException and not RequestAbortedException and not RequestBodyTooLargeException);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
