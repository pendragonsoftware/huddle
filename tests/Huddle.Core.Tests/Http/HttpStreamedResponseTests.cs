using Huddle.Core.Http;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Huddle.Core.Tests.Http;

public class HttpStreamedResponseTests
{
    private static HttpEndpointRegistration Get(Func<HttpRequestData, Task<HttpResponseData>> handler) =>
        new(StreamBody: false, MaxBodyBytes: null, handler);

    private static HttpResponseData StreamOf(Stream body, long? contentLength = null) =>
        new(200, string.Empty, "application/octet-stream") { BodyStream = body, ContentLength = contentLength };

    private static async Task<string> HashToHexAsync(Stream stream)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(await sha.ComputeHashAsync(stream));
    }

    [Fact]
    public async Task Multi_hundred_MB_streamed_download_arrives_intact_with_bounded_allocations()
    {
        const long payloadLength = 300L * 1024 * 1024;

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/download", "GET", Get(_ => Task.FromResult(StreamOf(new DeterministicStream(payloadLength))))));

        var expectedHash = await HashToHexAsync(new DeterministicStream(payloadLength));

        using var client = new HttpClient();
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        using var response = await client.GetAsync(new Uri(server.BaseAddress, "download"), HttpCompletionOption.ResponseHeadersRead);
        var hash = await HashToHexAsync(await response.Content.ReadAsStreamAsync());
        var allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payloadLength, response.Content.Headers.ContentLength);
        Assert.Equal(expectedHash, hash);

        // Both ends run in this process; buffering on either side would allocate at least the 300MB body.
        var allocated = allocatedAfter - allocatedBefore;
        Assert.True(allocated < 100L * 1024 * 1024, $"Expected bounded allocations but {allocated} bytes were allocated");
    }

    [Fact]
    public async Task Non_seekable_body_is_sent_chunked()
    {
        const long payloadLength = 4L * 1024 * 1024;

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/download", "GET", Get(_ => Task.FromResult(StreamOf(new NonSeekableStream(new DeterministicStream(payloadLength)))))));

        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri(server.BaseAddress, "download"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TransferEncodingChunked);
        Assert.Equal(await HashToHexAsync(new DeterministicStream(payloadLength)), await HashToHexAsync(await response.Content.ReadAsStreamAsync()));
    }

    [Fact]
    public async Task Body_starts_where_the_handler_positioned_the_stream()
    {
        const long payloadLength = 1024 * 1024;
        const int offset = 1000;
        var expected = new byte[payloadLength];
        new DeterministicStream(payloadLength).ReadExactly(expected);

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/download", "GET", Get(_ =>
            {
                var body = new MemoryStream(expected) { Position = offset };
                return Task.FromResult(StreamOf(body));
            })));

        using var client = new HttpClient();
        var bytes = await client.GetByteArrayAsync(new Uri(server.BaseAddress, "download"));

        Assert.Equal(expected.AsSpan(offset).ToArray(), bytes);
    }

    [Fact]
    public async Task Client_abort_mid_download_still_disposes_the_body()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/download", "GET", Get(_ =>
                Task.FromResult(StreamOf(new DisposeTrackingStream(new DeterministicStream(500L * 1024 * 1024), disposed))))));

        // Raw socket, so the connection can be dropped after reading only the start of the body.
        using (var tcpClient = new TcpClient())
        {
            await tcpClient.ConnectAsync("localhost", server.Port);
            var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET /download HTTP/1.1\r\nHost: localhost:{server.Port}\r\n\r\n"));
            await stream.ReadAtLeastAsync(new byte[64 * 1024], 64 * 1024);
        }

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class DisposeTrackingStream(Stream inner, TaskCompletionSource disposed) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            disposed.TrySetResult();
            base.Dispose(disposing);
        }
    }
}
