namespace Huddle.Client.Services.Interfaces
{
    public record ServiceInformation(string? InstanceName, string IpAddress, int Port, IDictionary<string, string> Attributes);

    internal interface IDiscoveryService : IDisposable
    {
        bool IsSearching { get; }

        event EventHandler<ServiceInformation>? ServiceDiscovered;
        event EventHandler? SearchFailed;
        event EventHandler<string?>? ServiceLost;

        void Search();

        void SearchContinuously();

        void StopSearching();
    }
}
