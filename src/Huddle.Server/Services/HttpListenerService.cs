using Huddle.Core.Http;
using Huddle.Server.Models;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Huddle.Server.Services;

internal partial class HttpListenerService(IServiceProvider serviceProvider, ILogger<HttpListenerService> logger) : INetworkListenerService, IDisposable
{
    private const string StickyPortPreferenceKey = "Huddle.Server.Http.StickyPort";

    private readonly HttpRequestProcessor _processor = new(logger);

    private HttpListener? _listener;
    private CancellationTokenSource _stoppingCts = new();
    private bool _cancelled = false;

    public bool IsListening => _listener?.IsListening ?? false;

    public string? IpAddress { get; private set; }

    public void Dispose()
    {
        _stoppingCts.Cancel();
        _listener?.Close();
    }

    public int Setup(string hostIpAddress, int port)
    {
        IpAddress = hostIpAddress;

        if (!HttpListener.IsSupported)
        {
            logger.LogError("HttpListener not supported");
        }

        // Auto-assigned ports are sticky: reusing the same port across launches keeps
        // Windows URL ACL reservations and firewall rules valid, instead of accumulating
        // one per launch.
        var explicitPort = port > 0;
        var assignedPort = explicitPort ? port : GetPersistedPort() ?? GetFreePort();

        if (!TryPreparePort(assignedPort))
        {
            var fallbackPort = GetFreePort();
            logger.LogWarning("HttpListenerService: Port {requestedPort} is unavailable - using automatically assigned port {fallbackPort} instead", assignedPort, fallbackPort);
            assignedPort = fallbackPort;
            TryPreparePort(assignedPort);
        }

        if (!explicitPort)
        {
            PersistPort(assignedPort);
        }

        _listener = new HttpListener();
        _listener.Prefixes.Clear();
        _listener.Prefixes.Add(GetPrefix(assignedPort));

        return assignedPort;
    }

    private int? GetPersistedPort()
    {
        try
        {
            var stored = Microsoft.Maui.Storage.Preferences.Default.Get(StickyPortPreferenceKey, 0);
            return stored > 0 ? stored : null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "HttpListenerService: Could not read the persisted port");
            return null;
        }
    }

    private void PersistPort(int port)
    {
        try
        {
            Microsoft.Maui.Storage.Preferences.Default.Set(StickyPortPreferenceKey, port);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "HttpListenerService: Could not persist assigned port {port}", port);
        }
    }

    public Task StartAsync()
    {
        if (_listener == null)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation("About to start listening...");

        _listener.Start();

        logger.LogInformation("Started listening...");

        _cancelled = false;
        if (_stoppingCts.IsCancellationRequested)
        {
            _stoppingCts = new CancellationTokenSource();
        }
        WaitForMessages(_listener);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _cancelled = true;
        // Fires RequestContext.Aborted in any in-flight handlers.
        _stoppingCts.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch { }

        logger.LogInformation("Stopped listening...");
        return Task.CompletedTask;
    }

    public void MapEndpoint(string path, string httpMethod, HttpEndpointOptions options, Func<RequestContext, Task<ResponseInformation>> action)
    {
        _processor.Map(path, httpMethod, new HttpEndpointRegistration(
            options.BodyMode == HttpBodyMode.Streamed,
            options.MaxBodyBytes,
            async requestData =>
            {
                var context = new RequestContext(
                    serviceProvider,
                    requestData.SourceHost,
                    new RequestInformation(requestData.Body, requestData.QueryString, requestData.Headers)
                    {
                        BodyStream = requestData.BodyStream,
                        ContentLength = requestData.ContentLength
                    })
                {
                    Aborted = requestData.Aborted
                };

                var (statusCode, responseText, responseContentType) = await action(context);
                return new HttpResponseData((int)statusCode, responseText, responseContentType);
            }));
    }

    private async void WaitForMessages(HttpListener listener)
    {
        while (!_cancelled)
        {
            logger.LogInformation("Waiting for client context...");
            try
            {
                var context = await listener.GetContextAsync();
                logger.LogInformation("Recieved client context...");

                // Task.Run, not a direct call: ProcessAsync's synchronous prefix would otherwise
                // run here - on whatever context started listening (the UI thread in a MAUI
                // host) - and block the next accept until it first awaits. Concurrent requests
                // must not queue behind each other's transfer.
                _ = Task.Run(() => _processor.ProcessAsync(context, _stoppingCts.Token));
            }
            catch (Exception ex)
            {
                // If cancelled just swallow the error - likely because the listener has been stopped
                if (!_cancelled)
                {
                    logger.LogError(ex, "Error getting context from listener");
                }
            }
        }
    }

    private string GetPrefix(int port)
    {
#if WINDOWS
        return $"http://+:{port}/";
#else
        return $"http://{IpAddress}:{port}/";
#endif
    }

    /// <summary>
    /// Confirms the port can actually be listened on, requesting a URL ACL reservation
    /// (one-time UAC prompt on Windows) if listening is denied. Returns false if the
    /// port is unavailable, e.g. already in use by another application.
    /// </summary>
    private bool TryPreparePort(int port)
    {
        if (ProbePort(port, out var accessDenied))
        {
            return true;
        }

        if (accessDenied)
        {
            EnsurePermissionForPort(port);
            return ProbePort(port, out _);
        }

        return false;
    }

    private bool ProbePort(int port, out bool accessDenied)
    {
        accessDenied = false;
        try
        {
            var probe = new HttpListener();
            probe.Prefixes.Add(GetPrefix(port));
            probe.Start();
            probe.Stop();
            return true;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            // ERROR_ACCESS_DENIED - no URL ACL reservation for this prefix.
            accessDenied = true;
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HttpListenerService: Could not listen on port {port}", port);
            return false;
        }
    }

    private int GetFreePort()
    {
        var tcpListener = new TcpListener(System.Net.IPAddress.Any, 0);
        tcpListener.Start();
        var assignedPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return assignedPort;
    }

    private void EnsurePermissionForPort(int port)
    {
#if WINDOWS
        var process = new System.Diagnostics.Process();
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
        startInfo.FileName = "cmd.exe";
        startInfo.UseShellExecute = true;
        startInfo.Arguments = $"/C netsh http add urlacl url=\"http://+:{port}/\" user=everyone";
        startInfo.Verb = "runas";
        startInfo.CreateNoWindow = true;
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit(30_000);

        // This seems to be required to allow time for the netsh command to propegate and not cause http listener access is denied
        Thread.Sleep(1000);
#endif
    }
}
