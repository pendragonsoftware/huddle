using Huddle.Client.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Devices.Enumeration;

namespace Huddle.Client.Platforms.Windows;

internal partial class DnssdDiscoveryService : IDiscoveryService, IDisposable
{
    private readonly ILogger _logger;

    private readonly string _serviceType;

    private DeviceWatcher? _watcher;
    private bool _stopWhenFound = false;
    private bool _startWhenStopped = false;
    private List<string?> _found = [];

    // A DeviceWatcher keeps raising Added/Updated/Removed after its initial enumeration
    // completes, so EnumerationCompleted still counts as actively searching.
    public bool IsSearching => _watcher?.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted;

    public event EventHandler<ServiceInformation>? ServiceDiscovered;
    public event EventHandler? SearchFailed;
    public event EventHandler<string?>? ServiceLost;

    public void Dispose()
    {
        _startWhenStopped = false;
        if (IsSearching)
        {
            _watcher?.Stop();
        }
    }

    public DnssdDiscoveryService(
        string serviceType,
        ILogger<DnssdDiscoveryService> logger)
    {
        _serviceType = serviceType;
        _logger = logger;
    }

    public void Search()
    {
        if (IsSearching)
        {
            return;
        }

        var watcher = GetOrSetupWatcher();
        _found.Clear();
        _stopWhenFound = true;

        StartWatcher(watcher);
    }

    public void SearchContinuously()
    {
        if (IsSearching)
        {
            return;
        }

        var watcher = GetOrSetupWatcher();
        _found.Clear();
        _stopWhenFound = false;

        StartWatcher(watcher);
    }

    public void StopSearching()
    {
        _startWhenStopped = false;
        if (IsSearching)
        {
            _watcher?.Stop();
        }
    }

    private DeviceWatcher GetOrSetupWatcher()
    {
        if (_watcher == null)
        {
            _watcher = DeviceInformation.CreateWatcher(
                    DnssdConstants.GetAqsQueryString(_serviceType),
                    DnssdConstants.PropertyKeys,
                    DeviceInformationKind.AssociationEndpointService);

            _watcher.Added += (s, a) => OnAddedDnssdService(a, _serviceType);
            _watcher.Updated += (s, a) => OnUpdatedDnssdService(a, _serviceType);
            _watcher.Removed += OnRemovedDnssdServiceAsync;
            _watcher.Stopped += OnWatcherStopped;
        }

        return _watcher;
    }

    private void StartWatcher(DeviceWatcher watcher)
    {
        try
        {
            switch (watcher.Status)
            {
                case DeviceWatcherStatus.Started:
                case DeviceWatcherStatus.EnumerationCompleted:
                    // Already watching the network; services keep raising Added/Updated.
                    return;
                case DeviceWatcherStatus.Stopping:
                    // A DeviceWatcher cannot start while stopping - restart from the Stopped event.
                    _startWhenStopped = true;
                    return;
                default:
                    watcher.Start();
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DnssdDiscoveryService: Failed to start searching");
            SearchFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnWatcherStopped(DeviceWatcher sender, object args)
    {
        if (_startWhenStopped)
        {
            _startWhenStopped = false;
            StartWatcher(sender);
        }
    }

    private void OnAddedDnssdService(DeviceInformation deviceInformation, string serviceName)
    {
        OnFoundDnssdService(deviceInformation.Properties, serviceName);
    }

    private void OnUpdatedDnssdService(DeviceInformationUpdate deviceInformation, string serviceName)
    {
        OnFoundDnssdService(deviceInformation.Properties, serviceName);
    }

    private void OnRemovedDnssdServiceAsync(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        var serviceName = args.Properties[DnssdConstants.SERVICENAME_PROPERTY].ToString();
        if (serviceName == null || !serviceName.Contains(_serviceType))
        {
            return;
        }

        var instanceName = args.Properties[DnssdConstants.INSTANCENAME_PROPERTY].ToString();
        _logger.LogInformation("DnssdDiscoveryService: Lost server {name}", instanceName);

        _found.Remove(instanceName);
        ServiceLost?.Invoke(this, instanceName);
    }

    private void OnFoundDnssdService(IReadOnlyDictionary<string, object> properties, string serviceType)
    {
        var serviceName = properties[DnssdConstants.SERVICENAME_PROPERTY].ToString();
        if (serviceName == null || !serviceName.Contains(serviceType))
        {
            return;
        }

        var instanceName = properties[DnssdConstants.INSTANCENAME_PROPERTY].ToString();
        if (_found.Contains(instanceName))
        {
            return;
        }
        else
        {
            _found.Add(instanceName);
        }

        var textAttributes = properties[DnssdConstants.TEXTATTRIBUTES_PROPERTY] as string[];
        var dictTextAttributes = textAttributes?
            .Select(x => x.Split("="))
            .ToDictionary(x => x[0], y => y[1]);


        var ipAddress = properties[DnssdConstants.IPADDRESS_PROPERTY].ToString();
        if (ipAddress == null)
        {
            _logger.LogWarning("DnssdDiscoveryService: Couldnt retrieve ip address from {serviceName}.", serviceName);
        }

        var sPort = properties[DnssdConstants.PORTNUMBER_PROPERTY].ToString();
        if (!int.TryParse(sPort, out var port))
        {
            _logger.LogWarning("DnssdDiscoveryService: Couldnt parse port from {serviceName}.", serviceName);
        }

        ServiceDiscovered?.Invoke(this, new ServiceInformation(instanceName, ipAddress ?? string.Empty, port, dictTextAttributes ?? []));

        if (_stopWhenFound)
        {
            StopSearching();
        }
    }
}
