namespace Huddle.Client
{
    public record ServerInformation(string IpAddress, int? HttpPort, int? QueuePort, int? MessagingPort);

    public enum SearchFailureReason
    {
        Timeout = 0,
        FailedToStartSearching = 1,
        FoundButCouldntParseInstanceName = 2
    }

    public interface IConnectedServer : IDisposable
    {
        string DeviceId { get; }
        string IpAddress { get; }
        int Port { get; }

        event EventHandler<string>? MessageReceived;

        Task<bool> SendMessageAsync(string message);
    }

    public interface IServerDiscoveryService : IDisposable
    {
        bool IsSearching { get; }
        string? DeviceId { get; }
        string? IpAddress { get; }
        IConnectedServer? ConnectedServer { get; }
        IEnumerable<ServerInformation> FoundServers { get; }

        event EventHandler<SearchFailureReason>? SearchFailed;
        event EventHandler<ServerInformation>? ServerDiscovered;
        event EventHandler<IConnectedServer>? ServerConnectionConfirmed;
        event EventHandler<IConnectedServer> ServerConnectionLost;

        Task SearchAsync(TimeSpan searchTimeout);

        void SearchContinuously();

        Task StopSearchingAsync();

        Task ConnectToServerAsync(ServerInformation serverInformation);

        Task DisconnectConnectedServerAsync();
    }
}
