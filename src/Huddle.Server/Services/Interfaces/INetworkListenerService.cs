using Huddle.Server.Models;

namespace Huddle.Server.Services.Interfaces;

internal interface INetworkListenerService
{
    bool IsListening { get; }

    string? IpAddress { get; }

    int Setup(string hostIpAddress, int port);

    Task StartAsync();

    Task StopAsync();

    void MapEndpoint(string path, string httpMethod, Func<RequestContext, Task<ResponseInformation>> action);
}
