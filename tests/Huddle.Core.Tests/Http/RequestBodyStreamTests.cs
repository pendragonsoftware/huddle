using Huddle.Core.Http;
using Huddle.Server.Models;

namespace Huddle.Core.Tests.Http;

public class RequestBodyStreamTests
{
    [Fact]
    public async Task Clean_end_of_body_returns_zero_without_throwing()
    {
        var stream = new RequestBodyStream(new MemoryStream(new byte[100]), contentLength: 100, maxBodyBytes: null);

        var total = await DrainAsync(stream);

        Assert.Equal(100, total);
        Assert.Equal(0, await stream.ReadAsync(new byte[16]));
        Assert.True(stream.ReachedEnd);
    }

    [Fact]
    public async Task Unknown_length_body_ends_cleanly_at_end_of_stream()
    {
        var stream = new RequestBodyStream(new MemoryStream(new byte[100]), contentLength: null, maxBodyBytes: null);

        var total = await DrainAsync(stream);

        Assert.Equal(100, total);
        Assert.True(stream.ReachedEnd);
    }

    [Fact]
    public async Task Truncated_body_throws_RequestAbortedException()
    {
        // The transport ends after 100 bytes although 250 were declared - the client went away.
        var stream = new RequestBodyStream(new MemoryStream(new byte[100]), contentLength: 250, maxBodyBytes: null);

        await Assert.ThrowsAsync<RequestAbortedException>(() => DrainAsync(stream));
    }

    [Fact]
    public async Task Body_over_the_size_limit_throws_RequestBodyTooLargeException()
    {
        var stream = new RequestBodyStream(new MemoryStream(new byte[2000]), contentLength: null, maxBodyBytes: 1000);

        var exception = await Assert.ThrowsAsync<RequestBodyTooLargeException>(() => DrainAsync(stream));

        Assert.Equal(1000, exception.MaxBodyBytes);
    }

    [Fact]
    public async Task Transport_errors_are_translated_to_RequestAbortedException()
    {
        var stream = new RequestBodyStream(new ThrowingStream(), contentLength: 100, maxBodyBytes: null);

        var exception = await Assert.ThrowsAsync<RequestAbortedException>(() => DrainAsync(stream));

        Assert.IsType<IOException>(exception.InnerException);
    }

    [Fact]
    public void Content_length_is_exposed_and_stream_is_read_once_forward_only()
    {
        var stream = new RequestBodyStream(new MemoryStream(new byte[10]), contentLength: 10, maxBodyBytes: null);

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(10, stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    private static async Task<long> DrainAsync(Stream stream)
    {
        var buffer = new byte[64];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            total += read;
        }
        return total;
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Connection reset");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
