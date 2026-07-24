using Android.Net.Nsd;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Huddle.Client.Platforms.Android;

internal class DiscoveryListener : Java.Lang.Object, NsdManager.IDiscoveryListener, IDisposable
{
    private readonly ILogger _logger;
    private readonly Action<string?, string, int, IDictionary<string, string>> _onFoundService;
    private readonly Action<string?> _serviceConnectionLost;
    private readonly Action _discoveryStopped;
    private readonly ResolveListener _resolveListener;

    public DiscoveryListener(
        ILogger logger,
        Action<string?, string, int, IDictionary<string, string>> foundService,
        Action<string?> serviceConnectionLost,
        Action discoveryStopped)
    {
        _logger = logger;

        _onFoundService = foundService;
        _discoveryStopped = discoveryStopped;
        _serviceConnectionLost = serviceConnectionLost;

        _resolveListener = new ResolveListener(logger);
    }

    public new void Dispose()
    {
        _resolveListener.Dispose();
    }

    public void OnDiscoveryStarted(string? serviceType)
    {
        _logger.LogDebug("DiscoveryListener:OnDiscoveryStarted {serviceType}", serviceType);
    }

    public void OnDiscoveryStopped(string? serviceType)
    {
        _logger.LogDebug("DiscoveryListener: OnDiscoveryStopped {serviceType}", serviceType);
    }

    public void OnServiceFound(NsdServiceInfo? serviceInfo)
    {
        _logger.LogDebug("NetworkDiscoveryService: Found server {name}", serviceInfo?.ServiceName);

        if (serviceInfo != null)
        {
            var attributes = serviceInfo.Attributes?.ToDictionary(x => x.Key, y => Encoding.ASCII.GetString(y.Value)) ?? [];
            var host = OperatingSystem.IsAndroidVersionAtLeast(34)
                ? serviceInfo.HostAddresses?.FirstOrDefault()?.ToString() ?? string.Empty
                : serviceInfo.Host?.ToString() ?? string.Empty;

            _onFoundService(serviceInfo.ServiceName, host, serviceInfo.Port, attributes);
        }
    }

    public void OnServiceLost(NsdServiceInfo? serviceInfo)
    {
        _logger.LogDebug("NetworkDiscoveryService: Server lost {name}", serviceInfo?.ServiceName);
        _serviceConnectionLost(serviceInfo?.ServiceName);
    }

    public void OnStartDiscoveryFailed(string? serviceType, NsdFailure errorCode)
    {
        _logger.LogDebug("NetworkDiscoveryService: Couldnt start discovery {errorCode}", errorCode);
        _discoveryStopped();
    }

    public void OnStopDiscoveryFailed(string? serviceType, NsdFailure errorCode)
    {
        _logger.LogDebug("NetworkDiscoveryService: Discovery failed {errorCode}", errorCode);
    }
}
