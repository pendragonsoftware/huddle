namespace Huddle.Client.Services
{
    internal class ServerClientSyncingService : IDisposable
    {
        private readonly IServerDiscoveryService _serverDiscoveryService;

        private List<HttpClient> _httpClients = [];
        private List<QueueClient> _queueClients = [];

        private string? _cachedServerIpAddress = null;
        private int? _cachedServerPort = 0;

        private string? _cachedQueueIpAddress = null;
        private int? _cachedQueuePort = 0;

        public ServerClientSyncingService(IServerDiscoveryService serverDiscoveryService)
        {
            _serverDiscoveryService = serverDiscoveryService;

            _serverDiscoveryService.ServerDiscovered += _serverDiscoveryService_ServerServiceFound;
        }

        public void Dispose()
        {
            _serverDiscoveryService.ServerDiscovered -= _serverDiscoveryService_ServerServiceFound;
        }

        public void RegisterHttpClient(HttpClient httpClient)
        {
            _httpClients.Add(httpClient);

            if (!string.IsNullOrEmpty(_cachedServerIpAddress) && _cachedServerPort != null)
            {
                httpClient.PointAtServer(_cachedServerIpAddress, _cachedServerPort.Value);
            }
        }

        public void RegisterQueueClient(QueueClient queueClient)
        {
            _queueClients.Add(queueClient);

            if (!string.IsNullOrEmpty(_cachedQueueIpAddress) && _cachedQueuePort != null)
            {
                queueClient.PointAtServer(_cachedQueueIpAddress, _cachedQueuePort.Value);
            }
        }

        private void _serverDiscoveryService_ServerServiceFound(object? sender, ServerInformation e)
        {
            _cachedServerIpAddress = e.IpAddress;
            _cachedServerPort = e.HttpPort;
                
            if (e.HttpPort.HasValue)
            {
                foreach (var httpClient in _httpClients)
                {
                    // Cater for if httpclients disposed of and still reference in list
                    // TODO: remove from the list in this case...
                    if (httpClient != null)
                    {
                        try
                        {
                            httpClient.PointAtServer(e.IpAddress, e.HttpPort.Value);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            _cachedQueueIpAddress = e.IpAddress;
            _cachedQueuePort = e.QueuePort;

            if (e.QueuePort.HasValue)
            {
                foreach (var queueClient in _queueClients)
                {
                    // Cater for if queueclients disposed of and still reference in list
                    // TODO: remove from the list in this case...
                    if (queueClient != null)
                    {
                        try
                        {
                            queueClient.PointAtServer(e.IpAddress, e.QueuePort.Value);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
        }
    }
}
