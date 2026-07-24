using Huddle.Client.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Network;

namespace Huddle.Client.Platforms.iOS;

internal class NWBrowserDiscoveryService(
    string serviceType,
    ILogger<NWBrowserDiscoveryService> logger) : IDiscoveryService, IDisposable
{
    private NWBrowser? _serviceBrowser;
    private bool _stopWhenFound = false;

    public bool IsSearching => _serviceBrowser?.IsActive ?? false;

    public event EventHandler<ServiceInformation>? ServiceDiscovered;
    public event EventHandler? SearchFailed;
    public event EventHandler<string?>? ServiceLost;

    public void Dispose()
    {
        _serviceBrowser?.Cancel();
        _serviceBrowser?.Dispose();
    }

    public void Search()
    {
        _stopWhenFound = true;
        SetupIfNotAlready();
    }

    public void SearchContinuously()
    {
        _stopWhenFound = false;
        SetupIfNotAlready();
    }

    public void StopSearching()
    {
        if (_serviceBrowser?.IsActive ?? false)
        {
            _serviceBrowser?.Cancel();
        }
    }

    private void SetupIfNotAlready()
    {
        if (_serviceBrowser != null)
        {
            return;
        }

        _serviceBrowser = new NWBrowser(NWBrowserDescriptor.CreateBonjourService($"_{serviceType}._udp"));

        _serviceBrowser.IndividualChangesDelegate = (_, after) =>
        {
            if (after != null)
            {
                logger.LogInformation("NWBrowserDiscoveryService: Discovered {hostname}", after.EndPoint.BonjourServiceName);

                IDictionary<string, string> attributes = new Dictionary<string, string>();
                /*if (after.TxtRecord != null)
                {
                    attributes = after.TxtRecord.ToDictionary();
                }*/
                ServiceDiscovered?.Invoke(this, new ServiceInformation(
                    after.EndPoint.BonjourServiceName,
                    after.EndPoint.Address,
                    after.EndPoint.PortNumber,
                    attributes));

                if (_stopWhenFound)
                {
                    _serviceBrowser?.Cancel();
                }
            }
            else
            {
                ServiceLost?.Invoke(this, serviceType);
            }
        };

        logger.LogInformation("NWBrowserDiscoveryService: Started searching...");
        _serviceBrowser?.SetDispatchQueue(CoreFoundation.DispatchQueue.MainQueue);

        try
        {
            _serviceBrowser?.Start();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NWBrowserDiscoveryService: Failed to start searching");
            SearchFailed?.Invoke(this, EventArgs.Empty);
        }
    }
}
