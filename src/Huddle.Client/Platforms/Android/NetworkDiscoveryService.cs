using Android.Content;
using Android.Net.Nsd;
using Huddle.Client.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace Huddle.Client.Platforms.Android;

internal class NetworkDiscoveryService(
    string serviceType,
    ILogger<NetworkDiscoveryService> logger) : IDiscoveryService
{
    private DiscoveryListener? _discoveryListener;
    private bool _stopWhenFound = false;
    private List<string?> _found = [];

    public bool IsSearching { get; private set; }

    public event EventHandler<ServiceInformation>? ServiceDiscovered;
    public event EventHandler? SearchFailed;
    public event EventHandler<string?>? ServiceLost;

    public void Dispose()
    {
        _discoveryListener?.Dispose();
    }

    public void Search()
    {
        if (IsSearching)
        {
            return;
        }

        _stopWhenFound = true;
        SetupAndSearch();
    }

    public void SearchContinuously()
    {
        if (IsSearching)
        {
            return;
        }

        _stopWhenFound = false;
        SetupAndSearch();
    }

    public void StopSearching()
    {
        IsSearching = false;
        if (TryGetNsdManager(out var nsdManager))
        {
            nsdManager?.StopServiceDiscovery(_discoveryListener);
        }
    }

    private static bool TryGetNsdManager(out NsdManager? nsdManager)
    {
        nsdManager = (NsdManager?)Application.Context.GetSystemService(Context.NsdService);
        if (nsdManager == null)
        {
            return false;
        }
        return true;
    }

    private bool SetupAndSearch()
    {
        if (!TryGetNsdManager(out var nsdManager) || nsdManager == null)
        {
            logger.LogError("NetworkDiscoveryService: Could not find nsd service");
            return false;
        }

        _found.Clear();
        _discoveryListener = new DiscoveryListener(
             logger,
             FoundServiceCheckIsServer,
             DiscoveryListener_ServiceLost,
             DiscoveryListener_DiscoveryStopped);

        IsSearching = true;
        nsdManager.DiscoverServices($"_{serviceType}._udp", NsdProtocol.DnsSd, _discoveryListener);

        return true;
    }

    private void FoundServiceCheckIsServer(string? name, string ipAddress, int port, IDictionary<string, string> attributes)
    {
        if (_stopWhenFound)
        {
            StopSearching();
        }

        if (_found.Contains(name))
        {
            return;
        }
        else
        {
            _found.Add(name);
        }

        ServiceDiscovered?.Invoke(this, new ServiceInformation(name, ipAddress, port, attributes));
    }

    private void DiscoveryListener_DiscoveryStopped()
    {
        SearchFailed?.Invoke(this, EventArgs.Empty);
    }

    private void DiscoveryListener_ServiceLost(string? instanceName)
    {
        _found.Remove(instanceName);
        ServiceLost?.Invoke(this, instanceName);
    }
}
