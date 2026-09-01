using Huddle.Core.Http;
using Huddle.Server.Models;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Huddle.Core.Tests.Http;

public class HttpStreamingTests
{
    private static HttpEndpointRegistration Streamed(Func<HttpRequestData, Task<HttpResponseData>> handler, long? maxBodyBytes = null) =>
        new(StreamBody: true, maxBodyBytes, handler);

    private static HttpEndpointRegistration Buffered(Func<HttpRequestData, Task<HttpResponseData>> handler, long? maxBodyBytes = null) =>
        new(StreamBody: false, maxBodyBytes, handler);

    private static HttpResponseData Ok(string body = "") => new(200, body, "text/plain");

    private static async Task<string> HashToHexAsync(Stream stream)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(await sha.ComputeHashAsync(stream));
    }

    /// <summary>Reads the stream to the end in fixed-size chunks, hashing incrementally.</summary>
    private static async Task<(string Hash, long Bytes)> HashBodyAsync(Stream body)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await body.ReadAsync(buffer)) > 0)
        {
            sha.AppendData(buffer, 0, read);
            total += read;
        }
        return (Convert.ToHexString(sha.GetHashAndReset()), total);
    }

    [Fact]
    public async Task Multi_hundred_MB_streamed_upload_arrives_intact_with_bounded_allocations()
    {
        const long payloadLength = 300L * 1024 * 1024;
        long? observedContentLength = null;

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(async request =>
            {
                observedContentLength = request.ContentLength;
                var (hash, _) = await HashBodyAsync(request.BodyStream!);
                return Ok(hash);
            })));

        var expectedHash = await HashToHexAsync(new DeterministicStream(payloadLength));

        using var client = new HttpClient();
        using var content = new StreamContent(new DeterministicStream(payloadLength));

        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var response = await client.PostAsync(new Uri(server.BaseAddress, "upload"), content);
        var allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedHash, await response.Content.ReadAsStringAsync());
        Assert.Equal(payloadLength, observedContentLength);

        // Client and server both run in this process; if either side buffered the payload the
        // delta would be at least the 300MB body. O(buffer) streaming stays far below that.
        var allocated = allocatedAfter - allocatedBefore;
        Assert.True(allocated < 100L * 1024 * 1024, $"Expected bounded allocations but {allocated} bytes were allocated");
    }

    [Fact]
    public async Task Chunked_upload_streams_with_unknown_content_length()
    {
        const long payloadLength = 8L * 1024 * 1024;
        var contentLengthObserved = new TaskCompletionSource<long?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(async request =>
            {
                contentLengthObserved.TrySetResult(request.ContentLength);
                var (hash, bytes) = await HashBodyAsync(request.BodyStream!);
                return Ok($"{hash}:{bytes}");
            })));

        var expectedHash = await HashToHexAsync(new DeterministicStream(payloadLength));

        using var client = new HttpClient();
        // A non-seekable stream has no computable length, so HttpClient sends it chunked.
        using var content = new StreamContent(new NonSeekableStream(new DeterministicStream(payloadLength)));

        var response = await client.PostAsync(new Uri(server.BaseAddress, "upload"), content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"{expectedHash}:{payloadLength}", await response.Content.ReadAsStringAsync());
        Assert.Null(await contentLengthObserved.Task);
    }

    [Fact]
    public async Task String_and_stream_routes_coexist_on_the_same_server()
    {
        await using var server = TestHttpServer.Start(processor =>
        {
            processor.Map("/echo", "POST", Buffered(request =>
            {
                Assert.Null(request.BodyStream);
                return Task.FromResult(Ok($"echo:{request.Body}"));
            }));
            processor.Map("/upload", "POST", Streamed(async request =>
            {
                Assert.Null(request.Body);
                var (hash, bytes) = await HashBodyAsync(request.BodyStream!);
                return Ok($"bytes:{bytes}");
            }));
        });

        using var client = new HttpClient();

        var echoResponse = await client.PostAsync(new Uri(server.BaseAddress, "echo"), new StringContent("hello", Encoding.UTF8));
        Assert.Equal(HttpStatusCode.OK, echoResponse.StatusCode);
        Assert.Equal("echo:hello", await echoResponse.Content.ReadAsStringAsync());

        var uploadResponse = await client.PostAsync(
            new Uri(server.BaseAddress, "upload"),
            new StreamContent(new DeterministicStream(1024 * 1024)));
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.Equal($"bytes:{1024 * 1024}", await uploadResponse.Content.ReadAsStringAsync());

        // The buffered route still works after the streamed one has run on the same server.
        var secondEcho = await client.PostAsync(new Uri(server.BaseAddress, "echo"), new StringContent("again", Encoding.UTF8));
        Assert.Equal("echo:again", await secondEcho.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Client_abort_mid_upload_fails_the_body_read_and_lets_the_handler_clean_up()
    {
        var handlerOutcome = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var partialFile = Path.Combine(Path.GetTempPath(), $"huddle-abort-test-{Guid.NewGuid():N}.bin");

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(async request =>
            {
                Exception? failure = null;
                var file = File.Create(partialFile);
                try
                {
                    await request.BodyStream!.CopyToAsync(file);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    await file.DisposeAsync();
                }

                if (failure != null)
                {
                    File.Delete(partialFile);
                    handlerOutcome.TrySetResult(failure);
                    throw failure;
                }

                handlerOutcome.TrySetResult(null);
                return Ok();
            })));

        // Raw socket, so the connection can be dropped after declaring a 50MB body and
        // sending only 64KB of it.
        using (var tcpClient = new TcpClient())
        {
            await tcpClient.ConnectAsync("localhost", server.Port);
            var stream = tcpClient.GetStream();
            var headers = $"POST /upload HTTP/1.1\r\nHost: localhost:{server.Port}\r\nContent-Length: 50000000\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers));
            await stream.WriteAsync(new byte[64 * 1024]);
            await stream.FlushAsync();
        }

        var exception = await handlerOutcome.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.IsType<RequestAbortedException>(exception);
        Assert.False(File.Exists(partialFile), "The half-written file should have been cleaned up");
    }

    [Fact]
    public async Task Client_abort_mid_chunked_upload_fails_the_body_read()
    {
        var handlerOutcome = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(async request =>
            {
                try
                {
                    await request.BodyStream!.CopyToAsync(Stream.Null);
                    handlerOutcome.TrySetResult(null);
                    return Ok();
                }
                catch (Exception ex)
                {
                    handlerOutcome.TrySetResult(ex);
                    throw;
                }
            })));

        using (var tcpClient = new TcpClient())
        {
            await tcpClient.ConnectAsync("localhost", server.Port);
            var stream = tcpClient.GetStream();
            var headers = $"POST /upload HTTP/1.1\r\nHost: localhost:{server.Port}\r\nTransfer-Encoding: chunked\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers));
            // Announce a 1MB chunk but only deliver a quarter of it before dropping the connection.
            await stream.WriteAsync(Encoding.ASCII.GetBytes("100000\r\n"));
            await stream.WriteAsync(new byte[256 * 1024]);
            await stream.FlushAsync();
        }

        var exception = await handlerOutcome.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.IsType<RequestAbortedException>(exception);
    }

    [Fact]
    public async Task Declared_body_over_the_route_limit_is_rejected_with_413_without_invoking_the_handler()
    {
        var handlerInvoked = false;

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(request =>
            {
                handlerInvoked = true;
                return Task.FromResult(Ok());
            }, maxBodyBytes: 1024)));

        using var client = new HttpClient();
        var response = await client.PostAsync(
            new Uri(server.BaseAddress, "upload"),
            new StreamContent(new DeterministicStream(4096)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.False(handlerInvoked);
    }

    [Fact]
    public async Task Chunked_body_over_the_route_limit_is_rejected_with_413_mid_stream()
    {
        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(async request =>
            {
                // The size cannot be checked up front (no Content-Length); the read throws
                // RequestBodyTooLargeException once the limit is crossed and the server
                // turns it into a 413.
                await request.BodyStream!.CopyToAsync(Stream.Null);
                return Ok();
            }, maxBodyBytes: 1024)));

        using var client = new HttpClient();
        // Small enough to fit in socket buffers, so the client can finish sending and then
        // read the early 413 without tripping over the closed connection.
        var response = await client.PostAsync(
            new Uri(server.BaseAddress, "upload"),
            new StreamContent(new NonSeekableStream(new DeterministicStream(32 * 1024))));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Buffered_route_respects_the_body_size_limit()
    {
        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/echo", "POST", Buffered(request => Task.FromResult(Ok(request.Body ?? "")), maxBodyBytes: 16)));

        using var client = new HttpClient();

        var okResponse = await client.PostAsync(new Uri(server.BaseAddress, "echo"), new StringContent("short"));
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
        Assert.Equal("short", await okResponse.Content.ReadAsStringAsync());

        var tooLargeResponse = await client.PostAsync(
            new Uri(server.BaseAddress, "echo"),
            new StringContent(new string('x', 1000)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLargeResponse.StatusCode);
    }

    [Fact]
    public async Task Query_string_and_headers_reach_a_streamed_handler()
    {
        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(async request =>
            {
                await request.BodyStream!.CopyToAsync(Stream.Null);
                return Ok($"{request.QueryString["fileName"]}|{request.Headers["X-Device-Id"]}");
            })));

        using var client = new HttpClient();
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(server.BaseAddress, "upload?fileName=holiday.jpg"))
        {
            Content = new StreamContent(new DeterministicStream(1024))
        };
        requestMessage.Headers.Add("X-Device-Id", "pixel-9");

        var response = await client.SendAsync(requestMessage);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("holiday.jpg|pixel-9", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Clean_end_of_body_is_distinguishable_from_an_abort()
    {
        var extraReadOutcome = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(async request =>
            {
                await request.BodyStream!.CopyToAsync(Stream.Null);
                // Reading again after a clean end keeps returning 0 - it never throws.
                extraReadOutcome.TrySetResult(await request.BodyStream.ReadAsync(new byte[16]));
                return Ok();
            })));

        using var client = new HttpClient();
        var response = await client.PostAsync(
            new Uri(server.BaseAddress, "upload"),
            new StreamContent(new DeterministicStream(1024)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await extraReadOutcome.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Unmapped_route_returns_404()
    {
        await using var server = TestHttpServer.Start(processor =>
            processor.Map("/upload", "POST", Streamed(request => Task.FromResult(Ok()))));

        using var client = new HttpClient();
        var response = await client.PostAsync(new Uri(server.BaseAddress, "nowhere"), new StringContent("x"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
