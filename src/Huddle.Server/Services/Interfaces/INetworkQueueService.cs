using Huddle.Server.Models;

namespace Huddle.Server.Services.Interfaces;

internal interface INetworkQueueService
{
    bool IsListening { get; }
    int? RequestedPort { get; set; }

    Task<int> StartAsync();

    Task StopAsync();

    void MapQueue(string queueName, Action<QueueContext> action);
}
