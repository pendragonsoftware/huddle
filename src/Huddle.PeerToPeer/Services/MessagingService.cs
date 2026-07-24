using Huddle.Client.Services.Interfaces;
using Huddle.Core.Services.Interfaces;
using Huddle.PeerToPeer.Services;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Huddle.PeerToPeer;

internal class MessagingService : IMessagingService
{
    private readonly string _serviceType;
    private readonly string _displayName;
    private readonly IIpAddressRetrievalService _ipAddressRetrievalService;
    private readonly IBroadcastService _broadcastService;
    private readonly IDiscoveryService _discoveryService;
    private readonly Core.Services.Interfaces.IMessagingService _messagingService;
    private readonly ILogger _logger;

    private readonly List<(string IpAddress, int Port, string DisplayName, bool LastSentMessageFailed)> _peers = [];

    public bool IsBroadcasting => _broadcastService.IsActive;
    public string DisplayName => _displayName;
    public IEnumerable<string> DisplayNames => _peers.Select(x => x.DisplayName).ToList();

    public event EventHandler<bool>? IsBroadcastingChanged;
    public event EventHandler<string>? PeerDiscovered;
    public event EventHandler<string>? PeerLost;
    public event EventHandler<(string Message, string DisplayName)>? MessageRecieved;

    public MessagingService(
        string serviceType,
        string displayName,
        IServiceProvider serviceProvider,
        IIpAddressRetrievalService ipAddressRetrievalService,
        IBroadcastService broadcastService,
        IDiscoveryService discoveryService,
        Core.Services.Interfaces.IMessagingService messagingService,
        ILogger<MessagingService> logger)
    {
        _serviceType = serviceType;
        _displayName = displayName;
        _ipAddressRetrievalService = ipAddressRetrievalService;
        _broadcastService = broadcastService;
        _discoveryService = discoveryService;
        _messagingService = messagingService;
        _logger = logger;

        _messagingService.MessageReceived += (o, e) =>
        {
            var handler = serviceProvider.GetService<IMessageHandler>();
            if (_peers.Any(x => x.IpAddress == e.FromIpAddress))
            {
                var peer = _peers.First(x => x.IpAddress == e.FromIpAddress);

                handler?.HandleMessageAsync(e.Message, peer.DisplayName);

                MessageRecieved?.Invoke(this, (e.Message, peer.DisplayName));
            }
        };

        _discoveryService.ServiceDiscovered += (o, e) =>
        {
            if (e.InstanceName != null)
            {
                if (!InstanceNameHelper.TryParse(e.InstanceName, out var displayName, out var ipAddress, out var port))
                {
                    _logger.LogWarning("Failed to parse found service {instanceName}", e.InstanceName);
                    return;
                }

                if (displayName == _displayName)
                {
                    // Dont add myself
                    return;
                }

                _peers.Add((ipAddress, port, displayName, false));
                PeerDiscovered?.Invoke(this, displayName);
            }
        };
        _discoveryService.ServiceLost += (o, e) =>
        {
            if (e == null)
            {
                return;
            }

            var index = _peers.FindIndex(x => x.DisplayName == e);
            if (index >= 0)
            {
                _peers.RemoveAt(index);
                PeerLost?.Invoke(this, e);
            }
        };
    }

    public void Dispose()
    {
        _messagingService.Dispose();
        _broadcastService.Dispose();
        _discoveryService.Dispose();
    }

    public async Task StartAsync()
    {
        var ipAddress = _ipAddressRetrievalService.GetIpAddress();
        if (ipAddress == null)
        {
            _logger.LogWarning("IP Address not found. Cannot start messaging service");
            return;
        }

        var port = await _messagingService.StartListeningAsync();

        var instanceName = InstanceNameHelper.Format(_displayName, ipAddress, port);
        await _broadcastService.StartAsync(_serviceType, instanceName);

        _discoveryService.SearchContinuously();

        IsBroadcastingChanged?.Invoke(this, true);
    }

    public async Task StopAsync()
    {
        await _messagingService.StopListeningAsync();

        await _broadcastService.StopAsync();

        _discoveryService.StopSearching();

        IsBroadcastingChanged?.Invoke(this, false);
    }

    public async Task<bool> SendAsync(string message, string displayName)
    {
        if (_peers.Any(x => x.DisplayName == displayName))
        {
            var peer = _peers.First(x => x.DisplayName == displayName);
            return await _messagingService.SendAsync(message, peer.IpAddress, peer.Port);
        }
        return false;
    }

    public async Task<SendToPeersResult> SendToAllAsync(string message)
    {
        var sendTasks = new List<Task<(bool Success, string DisplayName, string IpAddress)>>();
        foreach (var peer in _peers)
        {
            var task = Task.Run(async () =>
            {
                var result = await _messagingService.SendAsync(message, peer.IpAddress, peer.Port);
                return (result, peer.DisplayName, peer.IpAddress);
            });
            sendTasks.Add(task);
        }
        var results = await Task.WhenAll(sendTasks);

        var sentTo = new List<string>();
        var notSentTo = new List<string>();
        foreach (var result in results)
        {
            // Since we dont track connections, mark in our list that last message failed
            var index = _peers.FindIndex(x => x.IpAddress == result.IpAddress);
            if (index >= 0)
            {
                var item = _peers[index];
                _peers.RemoveAt(index);
                _peers.Insert(index, (item.IpAddress, item.Port, item.DisplayName, !result.Success));
            }

            if (result.Success)
            {
                sentTo.Add(result.DisplayName);
            }
            else
            {
                notSentTo.Add(result.DisplayName);
            }
        }

        return new SendToPeersResult([.. sentTo], [.. notSentTo]);
    }
}
