using Huddle.Client.Services.Interfaces;
using Huddle.Core;
using Huddle.Core.Services;
using Huddle.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Huddle.Client.Services
{
    internal class ServerDiscoveryService : IServerDiscoveryService
    {
        private readonly IDiscoveryService _discoveryService;
        private readonly IMessagingService _messagingService;
        private readonly IIpAddressRetrievalService _ipAddressRetrievalService;
        private readonly IDeviceInfoProvider _deviceIdProvider;
        private readonly ILogger _logger;
        private readonly bool _withMessaging = false;

        private CancellationTokenSource? _cancellationTokenSource;
        private List<ServerInformation> _foundServers = [];

        public bool IsSearching => _discoveryService.IsSearching;

        public string? DeviceId => _deviceIdProvider.GetDeviceIdentifier();
        public string? IpAddress => _ipAddressRetrievalService.GetIpAddress();
        public IConnectedServer? ConnectedServer { get; private set; }
        public IEnumerable<ServerInformation> FoundServers => _foundServers;

        public event EventHandler<SearchFailureReason>? SearchFailed;
        public event EventHandler<ServerInformation>? ServerDiscovered;
        public event EventHandler<IConnectedServer>? ServerConnectionConfirmed;
        public event EventHandler<IConnectedServer>? ServerConnectionLost;

        public ServerDiscoveryService(
           bool withMessaging,
           IDiscoveryService discoveryService,
           IMessagingService messagingService,
           IIpAddressRetrievalService ipAddressService,
           IDeviceInfoProvider deviceIdProvider,
           ILogger<ServerDiscoveryService> logger)
        {
            _withMessaging = withMessaging;
            _discoveryService = discoveryService;
            _messagingService = messagingService;
            _ipAddressRetrievalService = ipAddressService;
            _deviceIdProvider = deviceIdProvider;
            _logger = logger;

            _discoveryService.ServiceDiscovered += DiscoveryService_ServiceDiscovered;
            _discoveryService.SearchFailed += DiscoveryService_SearchFailed;
            _discoveryService.ServiceLost += DiscoveryService_ServiceLost;

            _messagingService.MessageReceived += MessagingService_MessageReceived;
        }

        public void Dispose()
        {
            _discoveryService.ServiceDiscovered -= DiscoveryService_ServiceDiscovered;
            _discoveryService.SearchFailed -= DiscoveryService_SearchFailed;
            _discoveryService.ServiceLost -= DiscoveryService_ServiceLost;

            _discoveryService.Dispose();

            _messagingService.MessageReceived -= MessagingService_MessageReceived;

            if (_messagingService.IsListening)
            {
                _messagingService.Dispose();
            }
        }

        public async Task SearchAsync(TimeSpan searchTimeout)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            _discoveryService.Search();

            await Task.Delay(searchTimeout, _cancellationTokenSource.Token);

            if (_discoveryService.IsSearching && !_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogWarning($"DiscoveryService: Timeout - found no server broadcasting");
                SearchFailed?.Invoke(this, SearchFailureReason.Timeout);
            }
        }

        public void SearchContinuously()
        {
            if (IsSearching)
            {
                return;
            }

            _discoveryService.SearchContinuously();
        }

        public async Task StopSearchingAsync()
        {
            if (_cancellationTokenSource != null)
            {
                await _cancellationTokenSource.CancelAsync();
            }
            _discoveryService.StopSearching();
        }

        public async Task ConnectToServerAsync(ServerInformation serverInformation)
        {
            if (_withMessaging)
            {
                if (!serverInformation.MessagingPort.HasValue)
                {
                    throw new Exception("No listening port provided from the server");
                }
                var myListeningPort = await _messagingService.StartListeningAsync(serverInformation.IpAddress);

                var deviceId = _deviceIdProvider.GetDeviceIdentifier();
                var myIpAddress = _ipAddressRetrievalService.GetIpAddress();
                await _messagingService.SendAsync(
                    ConnectionMessageFormatter.FormatRequest(deviceId, myIpAddress, myListeningPort),
                    serverInformation.IpAddress,
                    serverInformation.MessagingPort.Value);
            }
        }

        public async Task DisconnectConnectedServerAsync()
        {
            if (ConnectedServer != null)
            {
                await _messagingService.StopListeningAsync();

                ConnectedServer.Dispose();
                ConnectedServer = null;
            }
        }

        private void DiscoveryService_ServiceDiscovered(object? sender, ServiceInformation e)
        {
            if (InstanceNameHelper.TryParse(e.InstanceName, out string ipAddress, out int? httpPort, out int? queuePort, out int? listeningPort))
            {
                _logger.LogInformation("DiscoveryService: Found server {name} at {ipAddress}", e, ipAddress);

                var serverInformation = new ServerInformation(ipAddress, httpPort, queuePort, listeningPort);
                ServerDiscovered?.Invoke(this, serverInformation);

                _foundServers.Add(serverInformation);
            }
            /*else if (dictTextAttributes != null && InstanceNameParser.TryParseAttributes(dictTextAttributes, out ipAddress, out _, out httpPort, out queuePort, out listeningPort))
            {
                _logger.LogInformation("DiscoveryService: Found server {name} at {ipAddress}", instanceName, ipAddress);
                ServerServiceFound?.Invoke(this, new ServiceInformation(ipAddress, httpPort, queuePort, listeningPort));
            }*/
            else
            {
                _logger.LogWarning("DiscoveryService: Could not retrieve ipaddress and port (parsing error) for {instanceName}", e.InstanceName);
                SearchFailed?.Invoke(this, SearchFailureReason.FoundButCouldntParseInstanceName);
            }
        }

        private void DiscoveryService_SearchFailed(object? sender, EventArgs _)
        {
            _logger.LogDebug("DiscoveryService: Search failed");
            SearchFailed?.Invoke(this, SearchFailureReason.FailedToStartSearching);
        }

        private void DiscoveryService_ServiceLost(object? sender, string? e)
        {
            _logger.LogDebug("DiscoveryService: Server connection lost {instanceName}", e);
            if (_messagingService.IsListening)
            {
                _messagingService.StopListeningAsync();
            }
            if (ConnectedServer != null)
            {
                ServerConnectionLost?.Invoke(this, ConnectedServer);
                ConnectedServer.Dispose();
                ConnectedServer = null;
            }
        }

        private void MessagingService_MessageReceived(object? sender, UdpMessage e)
        {
            _logger.LogInformation("DnssdDiscoveryService: Recieved {message} from server", e.Message);

            if (ConnectionMessageParser.TryParseConfirmation(e.Message, out var deviceId))
            {
                var connectedServer = new ConnectedServer(deviceId, e.FromIpAddress, e.FromPort, _messagingService, _logger);
                ConnectedServer = connectedServer;

                ServerConnectionConfirmed?.Invoke(this, connectedServer);
            }
        }
    }
}
