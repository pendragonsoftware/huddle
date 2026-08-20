using Huddle.Server.Models;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Huddle.Server.Services;

internal partial class HttpListenerService(IServiceProvider serviceProvider, ILogger<HttpListenerService> logger) : INetworkListenerService, IDisposable
{
    private readonly List<Client> _clients = [];
    private readonly Dictionary<(string Path, string HttpMethod), Func<RequestContext, Task<ResponseInformation>>> _endpoints = [];

    private HttpListener? _listener;
    private bool _cancelled = false;

    public bool IsListening => _listener?.IsListening ?? false;

    public string? IpAddress { get; private set; }

    public void Dispose()
    {
        _listener?.Close();
    }

    public int Setup(string hostIpAddress, int port)
    {
        IpAddress = hostIpAddress;

        if (!HttpListener.IsSupported)
        {
            logger.LogError("HttpListener not supported");
        }

        var assignedPort = port > 0 ? port : GetFreePort();

        if (!TryPreparePort(assignedPort))
        {
            var fallbackPort = GetFreePort();
            logger.LogWarning("HttpListenerService: Port {requestedPort} is unavailable - using automatically assigned port {fallbackPort} instead", assignedPort, fallbackPort);
            assignedPort = fallbackPort;
            TryPreparePort(assignedPort);
        }

        _listener = new HttpListener();
        _listener.Prefixes.Clear();
        _listener.Prefixes.Add(GetPrefix(assignedPort));

        return assignedPort;
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
        WaitForMessages(_listener);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _cancelled = true;
        try
        {
            _listener?.Stop();
        }
        catch { }

        logger.LogInformation("Stopped listening...");
        return Task.CompletedTask;
    }

    public void MapEndpoint(string path, string httpMethod, Func<RequestContext, Task<ResponseInformation>> action)
    {
        _endpoints.Add((path, httpMethod), action);
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

                var client = new Client(context, logger, HandleMessage);
                _clients.Add(client);
                var clientTask = client.RunAsync();
                _ = clientTask.ContinueWith(_ => _clients.Remove(client));
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

    private async Task<ResponseInformation> HandleMessage(
        string? path,
        string httpMethod,
        string? body,
        string sourceHost,
        Dictionary<string, string> queryString,
        Dictionary<string, string> headers)
    {
        logger.LogInformation("Message handled {httpMethod}:{path} with {body} from {sourceHost}. {@queryString}, {@headers}", path, httpMethod, body, sourceHost, queryString, headers);

        if (_endpoints.TryGetValue((path ?? "/", httpMethod), out var action))
        {
            return await action(new RequestContext(serviceProvider, sourceHost, new RequestInformation(body, queryString, headers)));
        }

        return new ResponseInformation(HttpStatusCode.NotFound, string.Empty, string.Empty);
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

    private class Client(
            HttpListenerContext context,
            ILogger logger,
            Func<string?, string, string?, string, Dictionary<string, string>, Dictionary<string, string>, Task<ResponseInformation>> messageRecieved)
    {
        public async Task RunAsync()
        {
            var request = context.Request;
            var response = context.Response;

            var requestMessage = ReadStreamAsString(request.InputStream, request.ContentEncoding);
            var dictionaryQueryString = ToDictionary(request.QueryString);
            var dictionaryHeaders = ToDictionary(request.Headers);
            var soureceIpAddress = request.UserHostName;

            string? rawUrlMinusQueryParams = null;
            if (request.RawUrl != null)
            {
                var rawUrlSplit = request.RawUrl.Split("?");
                rawUrlMinusQueryParams = rawUrlSplit[0];
            }

            try
            {
                var (statusCode, responseText, responseContentType) = await messageRecieved(
                    rawUrlMinusQueryParams,
                    request.HttpMethod,
                    requestMessage,
                    soureceIpAddress,
                    dictionaryQueryString,
                    dictionaryHeaders);
                    response.StatusCode = (int)statusCode;

                if (!string.IsNullOrEmpty(responseText))
                {
                    var buffer = Encoding.UTF8.GetBytes(responseText);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = responseContentType;

                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                var buffer = Encoding.UTF8.GetBytes(ex.Message);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/plain";

                response.OutputStream.Write(buffer, 0, buffer.Length);

                logger.LogError(ex, "Error performing {@request}", request);
            }

            response.OutputStream.Close();
        }

        private string? ReadStreamAsString(Stream stream, Encoding contentEncoding)
        {
            string? requestMessage;
            try
            {
                using var body = stream;
                using (var reader = new StreamReader(body, contentEncoding))
                
                requestMessage = reader.ReadToEnd();
                return requestMessage;
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "Error reading input stream for request");
                return null;
            }
        }

        private static Dictionary<string, string> ToDictionary(NameValueCollection nameValueCollection)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (var key in nameValueCollection.AllKeys)
            {
                if (key != null)
                {
                    dictionary.Add(key, nameValueCollection[key] ?? string.Empty);
                }
            }
            return dictionary;
        }
    }
}
