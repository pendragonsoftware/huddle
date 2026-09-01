namespace Huddle.Core.Tests.Http;

/// <summary>
/// Produces <paramref name="length"/> bytes of deterministic pseudo-random data (xorshift64)
/// without ever holding the payload in memory, so tests can send and verify multi-hundred-MB
/// bodies with O(buffer) allocations. Reports <see cref="CanSeek"/> = true so HttpClient's
/// StreamContent computes a Content-Length; wrap in <see cref="NonSeekableStream"/> to force
/// chunked transfer encoding instead.
/// </summary>
internal sealed class DeterministicStream(long length, ulong seed = 88172645463325252UL) : Stream
{
    private long _position;
    private ulong _state = seed == 0 ? 1UL : seed;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => length;

    public override long Position
    {
        get => _position;
        set
        {
            if (value == _position)
            {
                return;
            }
            if (value == 0)
            {
                _position = 0;
                _state = seed == 0 ? 1UL : seed;
                return;
            }
            throw new NotSupportedException("Only rewinding to the start is supported");
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = length - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        var toWrite = (int)Math.Min(count, remaining);
        for (var i = 0; i < toWrite; i++)
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            buffer[offset + i] = (byte)_state;
        }

        _position += toWrite;
        return toWrite;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Begin)
        {
            Position = offset;
            return _position;
        }
        throw new NotSupportedException();
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>Hides seekability so StreamContent cannot compute a Content-Length.</summary>
internal sealed class NonSeekableStream(Stream inner) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
