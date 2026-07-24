using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.ServiceDiscovery.Dnssd;
using Windows.Networking.Sockets;

namespace Huddle.Server.Platforms.Windows;

internal partial class DnssdBroadcastService(int broadcastPort, ILogger<DnssdBroadcastService> logger) : IBroadcastService
{
    private DnssdServiceInstance? _service;
    private StreamSocketListener? _listener;

    public bool IsActive { get; private set; }

    public void Dispose()
    {
        _listener?.Dispose();
    }

    public async Task StartAsync(string serviceType, string instanceName)
    {
        // TODO: currently cant de-register and it hangs on re-regestering, sorry just dont actually stop at the moment...
        if (_listener != null)
        {
            logger.LogInformation("DnssdBroadcastService: (Re-)registered successfully with {name}", _service?.DnssdServiceInstanceName ?? instanceName);
            return;
        }

        var hostName = NetworkInformation
            .GetHostNames()
            .Where(x => x.Type == HostNameType.DomainName)
            .FirstOrDefault(x => x.RawName.Contains(".local"));
        if (hostName == null)
        {
            logger.LogError("DnssdBroadcastService: No host name found");
            return;
        }

        _listener = new StreamSocketListener();
        await _listener.BindServiceNameAsync(broadcastPort.ToString());

        _service = new DnssdServiceInstance(
            dnssdServiceInstanceName: $"{instanceName}._{serviceType}._udp.local.",
            hostName: hostName,
            port: UInt16.Parse(_listener.Information.LocalPort)
        );

        //AddAttributes(_service, ipAddress, httpPort, queuePort, messagingPort);

        var registration = await _service.RegisterStreamSocketListenerAsync(_listener);

        if (registration.Status == DnssdRegistrationStatus.Success)
        {
            IsActive = true;
            logger.LogInformation("DnssdBroadcastService: Registered successfully with {name}", _service.DnssdServiceInstanceName);
        }
        else
        {
            logger.LogError(
                "DnssdBroadcastService: Registration error for name {name} ({status})",
                _service.DnssdServiceInstanceName,
                registration.Status);
        }
    }

    public Task StopAsync()
    {
        IsActive = false;
        // TODO: currently cant de-register - see comment at top of start
        return Task.CompletedTask;
    }

    /*private void AddAttributes(DnssdServiceInstance service, string ipAddress, int? httpPort, int? queuePort, int? listeningPort)
    {
        var deviceId = deviceInfoProvider.GetDeviceIdentifier() ?? string.Empty;
        if (!string.IsNullOrEmpty(deviceId))
        {
            service.TextAttributes.Add("DeviceId", deviceId);
        }
        service.TextAttributes.Add("IpAddress", ipAddress);
        if (httpPort.HasValue)
        {
            service.TextAttributes.Add("HttpPort", httpPort.Value.ToString());
        }
        if (queuePort.HasValue)
        {
            service.TextAttributes.Add("QueuePort", queuePort.Value.ToString());
        }
        if (listeningPort.HasValue)
        {
            service.TextAttributes.Add("ListeningPort", listeningPort.Value.ToString());
        }
    }*/
}
