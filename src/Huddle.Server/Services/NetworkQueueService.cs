using Huddle.Core.Services.Interfaces;
using Huddle.Server.Models;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Huddle.Server.Services;

internal partial class NetworkQueueService(IServiceProvider serviceProvider, IMessagingService messagingService, ILogger<NetworkQueueService> logger) : INetworkQueueService, IDisposable
{
    private readonly List<Client> _clients = [];
    private readonly Dictionary<string, Action<QueueContext>> _queues = [];

    private CancellationTokenSource? _cancellationTokenSource;
    private bool _running = false;

    public bool IsListening => _running;
    public int? RequestedPort { get; set; }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        messagingService.Dispose();
    }

    public async Task<int> StartAsync()
    {
        var givenPort = await messagingService.StartListeningAsync(null, RequestedPort);

        logger.LogInformation("About to start listening...");
        _running = true;
        _cancellationTokenSource = new();

        messagingService.MessageReceived += MessagingService_MessageReceived;

        logger.LogInformation("Started listening...");

        return givenPort;
    }

    public async Task StopAsync()
    {
        _running = false;
        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource = null;
        }
       
        await messagingService.StopListeningAsync();
        messagingService.MessageReceived -= MessagingService_MessageReceived;

        logger.LogInformation("Stopped listening...");
    }

    public void MapQueue(string queueName, Action<QueueContext> action)
    {
        _queues.Add(queueName, action);
    }

    private void MessagingService_MessageReceived(object? sender, UdpMessage e)
    {
        var client = new Client(e.Message, HandleMessage);
        _clients.Add(client);
        var clientTask = client.RunAsync();
        _ = clientTask.ContinueWith(_ => _clients.Remove(client));
    }

    private void HandleMessage(string queueName, string message, string sourceHost)
    {
        logger.LogInformation("Message handled {queueName} with {message} from {sourceHost}", queueName, message, sourceHost);

        if (_queues.TryGetValue(queueName, out var action))
        {
            action(new QueueContext(serviceProvider, sourceHost, message));
        }
        else
        {
            logger.LogWarning("No queue found matching name {queueName}. Request from {sourceHost} with {message}", queueName, sourceHost, message);
        }
    }

    private class Client(
            string message,
            Action<string, string, string> messageHandled)
    {
        public Task RunAsync()
        {
            if (!Huddle.Core.Services.QueueMessageParser.TryParse(message, out var queueName, out var sourceIpAddress, out var messageContent))
            {
                throw new Exception("Error parsing message format. Should be <queue name>:<sourceip address>:<message>");
            }

            messageHandled(queueName, messageContent, sourceIpAddress);

            return Task.CompletedTask;
        }
    }
}
