using Huddle.Core.Platforms.iOS;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Network;

namespace Huddle.Server.Platforms.iOS;

internal class NWBroadcastService(int broadcastPort, ILogger<NWBroadcastService> logger) : NWConnectionMessagingService(logger), IBroadcastService
{
    private string _serviceType = string.Empty;
    private string _instanceName = string.Empty;

    private NWAdvertiseDescriptor? _serviceAdvertiser;

    public bool IsActive => IsListening;

    public async Task StartAsync(string serviceType, string instanceName)
    {
        _serviceType = serviceType;
        _instanceName = instanceName;

        await StartListeningAsync(null, broadcastPort);
    }

    public async Task StopAsync()
    {
        await StopListeningAsync();
    }

    protected override void ListenerBeforeStart(NWListener listener)
    {
        _serviceAdvertiser = NWAdvertiseDescriptor.CreateBonjourService(_instanceName, $"_{_serviceType}._udp")!;
        _serviceAdvertiser.NoAutoRename = true;

        _serviceAdvertiser.TxtRecord = NWTxtRecord.CreateDictionary()!;
        //AddAttributes(_serviceAdvertiser, _ipAddress, _httpPort, _queuePort, _messagingPort);

        listener.SetAdvertiseDescriptor(_serviceAdvertiser);

        logger.LogInformation("NWBroadcastService: Started advertising... {serviceName}", _instanceName);
    }

    protected override bool ShouldAcceptConnection(NWConnection _)
    {
        return false;
    }

    /*private void AddAttributes(NWAdvertiseDescriptor serviceAdvertiser, string ipAddress, int? httpPort, int? queuePort, int? listeningPort)
    {
        var deviceId = deviceInfoProvider.GetDeviceIdentifier() ?? string.Empty;
        if (!string.IsNullOrEmpty(deviceId))
        {
            serviceAdvertiser.TxtRecord.Add("DeviceId", deviceId);
        }
        serviceAdvertiser.TxtRecord.Add("IpAddress", ipAddress);
        if (httpPort.HasValue)
        {
            serviceAdvertiser.TxtRecord.Add("HttpPort", httpPort.Value.ToString());
        }
        if (queuePort.HasValue)
        {
            serviceAdvertiser.TxtRecord.Add("QueuePort", queuePort.Value.ToString());
        }
        if (listeningPort.HasValue)
        {
            serviceAdvertiser.TxtRecord.Add("ListeningPort", listeningPort.Value.ToString());
        }
    }*/
}
