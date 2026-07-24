namespace Huddle.Server.Services.Interfaces;

internal interface IBroadcastService : IDisposable
{
    bool IsActive { get; }

    Task StartAsync(string serviceType, string instanceName);

    Task StopAsync();
}
