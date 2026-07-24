using Huddle.Core.Services.Interfaces;
using Huddle.Server.Models;
using Huddle.Server.Services;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Huddle.Server.Implementations;

public class HuddleInstance : IMobileServer
{
    private readonly IBroadcastService _broadcastService;
    private readonly ServerClientMessagingService? _messagingService;
    private readonly HuddleHttpApi? _httpApi;
    private readonly HuddleQueue? _queue;

    private int? _messagingPort;

    public bool IsBroadcasting { get; private set; }
    public string? IpAddress { get; private set; }
    public string? DeviceId { get; private set; }
    public string ServiceName { get; private set; }

    public IHttpApi? HttpApi => _httpApi;
    public IQueue? Queue => _queue;
    public int? MessagingPort => _messagingPort;

    public event EventHandler? StartedBroadcasting;
    public event EventHandler<(string Message, string IpAddress)>? RecievedMessageFromClient;
    public event EventHandler<(string DeviceId, string IpAddress)>? ClientConnected;

    internal HuddleInstance(
        IServiceProvider serviceProvider,
        IIpAddressRetrievalService ipAddressRetrievalService,
        IDeviceInfoProvider deviceInfoProvider,
        IBroadcastService broadcastService,
        ServerClientMessagingService? messagingService,
        INetworkListenerService networkListenerService,
        INetworkQueueService? networkQueueService,
        ILogger<HuddleQueue> loggerQueue,
        string serviceName,
        int? httpPort,
        int? queuePort,
        bool dlq,
        List<(string Path, string HttpMethod, Func<RequestContext, Task<ResponseInformation>> Handler)> endpoints,
        Dictionary<string, Type> handlers)
    {
        _broadcastService = broadcastService;

        _messagingService = messagingService;
        _messagingService?.RecievedMessage += (o, e) =>
        {
            var handler = serviceProvider.GetService<IMessageHandler>();
            handler?.HandleMessageAsync(e.Message, e.IpAddress);

            RecievedMessageFromClient?.Invoke(this, e);
        };
        _messagingService?.ClientConnected += (o, e) =>
        {
            ClientConnected?.Invoke(this, e);
        };

        IpAddress = ipAddressRetrievalService.GetIpAddress();
        DeviceId = deviceInfoProvider.GetDeviceIdentifier();

        ServiceName = serviceName;

        if (httpPort.HasValue)
        {
            _httpApi = new HuddleHttpApi(this, networkListenerService, httpPort.Value, endpoints);
        }
        if (queuePort.HasValue)
        {
            if (networkQueueService is null)
            {
                throw new Exception("Queue port is specified but no INetworkQueueService is provided");
            }

            _queue = new HuddleQueue(this, serviceProvider, networkQueueService, loggerQueue, queuePort.Value, dlq, handlers);
        }
    }

    public async Task StartAsync()
    {
        if (IpAddress == null)
        {
            throw new Exception("Must have an IP Address to use this instance");
        }

        if (_httpApi != null)
        {
            await _httpApi.StartAsync();
        }
        if (_queue != null)
        {
            await _queue.StartAsync();
        }

        if (_messagingService != null)
        {
            _messagingPort = await _messagingService.StartListeningAsync();
        }

        if (!IsBroadcasting)
        {
            var instanceName = InstanceNameHelper.Format(IpAddress, _httpApi?.Port, _queue?.Port, _messagingPort);
            await _broadcastService.StartAsync(ServiceName, instanceName);

            IsBroadcasting = true;
            StartedBroadcasting?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task StopAsync()
    {
        if (IsBroadcasting)
        {
            await _broadcastService.StopAsync();
            IsBroadcasting = false;
        }
        
        if (_httpApi != null)
        {
            await _httpApi.StopAsync();
        }
        if (_queue != null)
        {
            await _queue.StopAsync();
        }

        if (_messagingService != null)
        {
            await _messagingService.StopListeningAsync();
        }
    }

    public async Task<SendToPeersResult> SendMessageToClientsAsync(string message)
    {
        if (_messagingService == null)
        {
            throw new Exception("Messaging service is not enabled for this server instance");
        }

        return await _messagingService.SendMessageToPeersAsync(message);
    }
}
