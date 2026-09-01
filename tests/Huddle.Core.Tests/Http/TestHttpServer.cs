using Huddle.Core.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace Huddle.Core.Tests.Http;

/// <summary>
/// Minimal host around <see cref="HttpRequestProcessor"/>: a real <see cref="HttpListener"/>
/// on localhost with the same accept-and-dispatch pattern Huddle.Server uses, so tests
/// exercise the actual request pipeline over real HTTP connections.
/// </summary>
internal sealed class TestHttpServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly Task _acceptLoop;

    public int Port { get; }

    public Uri BaseAddress => new($"http://localhost:{Port}/");

    private TestHttpServer(HttpListener listener, int port, HttpRequestProcessor processor)
    {
        _listener = listener;
        Port = port;
        _acceptLoop = Task.Run(async () =>
        {
            while (!_stoppingCts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch
                {
                    // Listener stopped.
                    break;
                }

                _ = Task.Run(() => processor.ProcessAsync(context, _stoppingCts.Token));
            }
        });
    }

    public static TestHttpServer Start(Action<HttpRequestProcessor> configureEndpoints)
    {
        var processor = new HttpRequestProcessor(NullLogger.Instance);
        configureEndpoints(processor);

        // localhost prefixes do not need a URL ACL reservation, so this runs unelevated.
        var random = new Random();
        for (var attempt = 0; ; attempt++)
        {
            var port = random.Next(20000, 60000);
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            try
            {
                listener.Start();
                return new TestHttpServer(listener, port, processor);
            }
            catch (HttpListenerException) when (attempt < 20)
            {
                listener.Close();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stoppingCts.Cancel();
        try
        {
            _listener.Stop();
        }
        catch
        {
        }

        try
        {
            await _acceptLoop;
        }
        catch
        {
        }

        _listener.Close();
        _stoppingCts.Dispose();
    }
}
