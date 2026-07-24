using Huddle.Server.Builders;
using Huddle.Server.Models;
using Huddle.Server.Queue;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Huddle.Server.Implementations;

public partial class HuddleQueue : IQueue, IDisposable
{
    private readonly HuddleInstance _server;
    private readonly IServiceProvider _serviceProvider;
    private readonly INetworkQueueService _networkQueueService;
    private readonly ILogger _logger;
    private readonly QueueWithDlq _queue;

    private bool _stopped = true;

    public bool IsRunning => _networkQueueService.IsListening;
    public string? IpAddress => _server.IpAddress;
    public int Port { get; private set; }

    internal HuddleQueue(
        HuddleInstance server,
        IServiceProvider serviceProvider,
        INetworkQueueService udpListenerService,
        ILogger<HuddleQueue> logger,
        int port,
        bool withDlq,
        Dictionary<string, Type> handlers)
    {
        _server = server;
        _serviceProvider = serviceProvider;
        _networkQueueService = udpListenerService;
        _logger = logger;

        _queue = new QueueWithDlq(withDlq);
        _queue.AddedToDlq += Queue_AddedToDlq;

        Port = port;
        Setup(handlers);
    }

    public void Dispose()
    {
        _queue.AddedToDlq -= Queue_AddedToDlq;
        GC.SuppressFinalize(this);
    }

    public async Task StartAsync()
    {
        if (!_networkQueueService.IsListening)
        {
            Port = await _networkQueueService.StartAsync();
        }

        _stopped = false;
        _ = Task.Run(StartQueueLoop);
    }

    public async Task StopAsync()
    {
        await _networkQueueService.StopAsync();
        _stopped = true;
    }

    public Task PauseAsync()
    {
        _stopped = true;
        return Task.CompletedTask;
    }

    public IEnumerable<QueueContext> PollForMessages(int? limit = null) => _queue.PollForMessages(limit);

    public bool RemoveFromQueue(QueueContext context) => _queue.RemoveFromQueue(context);

    public IEnumerable<QueueContext> PollDlqForMessages(int? limit = null) => _queue.PollDlqForMessages(limit);

    public bool RemoveFromDlq(QueueContext context) => _queue.RemoveFromDlq(context);

    private void Setup(Dictionary<string, Type> handlers)
    {
        foreach (var handlerMapping in handlers)
        {
            var handler = (IQueueHandler)_serviceProvider.GetRequiredService(handlerMapping.Value)
                ?? throw new Exception($"DI Error: Handler of type {handlerMapping.Value.Name} not registered with service provider");
            _networkQueueService.MapQueue(handlerMapping.Key, context => MessageRecieved(context, handler));
        }
    }

    private void Queue_AddedToDlq(object? sender, Exception e)
    {
        _logger.LogError(e, "Queue Message Handling Error: Added to DLQ");
    }

    private void StartQueueLoop()
    {
        while (!_stopped)
        {
            try
            {
                _queue.Dequeue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queue Message Handling Error");
            }
        }
    }

    private void MessageRecieved(QueueContext context, IQueueHandler handler)
    {
        _queue.Enqueue(context, handler);
    }
}
