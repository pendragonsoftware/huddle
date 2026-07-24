using Huddle.Server.Models;
using Huddle.Server.Services.Interfaces;

namespace Huddle.Server.Implementations;

public class HuddleHttpApi : IHttpApi
{
    private readonly HuddleInstance _server;
    private readonly INetworkListenerService _networkListenerService;

    public bool IsRunning => _networkListenerService.IsListening;
    public string? IpAddress => _server.IpAddress;
    public int Port { get; private set; }

    internal HuddleHttpApi(
        HuddleInstance server,
        INetworkListenerService tcpListenerService,
        int port,
        List<(string Path, string HttpMethod, Func<RequestContext, Task<ResponseInformation>> Handler)> endpoints)
    {
        _server = server;
        _networkListenerService = tcpListenerService;

        Port = Setup(port, endpoints);
    }

    public async Task StartAsync()
    {
        await _networkListenerService.StartAsync();
    }

    public async Task StopAsync()
    {
        await _networkListenerService.StopAsync();
    }

    private int Setup(int port, List<(string Path, string HttpMethod, Func<RequestContext, Task<ResponseInformation>> Handler)> endpoints)
    {
        if (IpAddress == null)
        {
            throw new Exception("Must have an IP Address to use this instance");
        }

        var assignedPort = _networkListenerService.Setup(IpAddress, port);

        foreach (var endpoint in endpoints)
        {
            _networkListenerService.MapEndpoint(endpoint.Path, endpoint.HttpMethod, endpoint.Handler);
        }

        return assignedPort;
    }
}
