using Huddle.Server.Models;
using Huddle.Server.Services;

namespace Huddle.Server;

public interface IMobileServer
{
    string? DeviceId { get; }
    string? IpAddress { get; }
    bool IsBroadcasting { get; }
    IHttpApi? HttpApi { get; }
    IQueue? Queue { get; }
    int? MessagingPort { get; }

    event EventHandler? StartedBroadcasting;

    Task StartAsync();
    Task StopAsync();

    Task<SendToPeersResult> SendMessageToClientsAsync(string message);

    event EventHandler<(string Message, string IpAddress)>? RecievedMessageFromClient;
    event EventHandler<(string DeviceId, string IpAddress)>? ClientConnected;
}

public interface INetworkService
{
    string? IpAddress { get; }
    bool IsRunning { get; }
    int Port { get; }

    Task StartAsync();
    Task StopAsync();
}

public interface IHttpApi : INetworkService
{
}

public interface IQueue : INetworkService
{
    Task PauseAsync();
    IEnumerable<QueueContext> PollForMessages(int? limit = null);
    bool RemoveFromQueue(QueueContext context);
    IEnumerable<QueueContext> PollDlqForMessages(int? limit = null);
    bool RemoveFromDlq(QueueContext context);
}