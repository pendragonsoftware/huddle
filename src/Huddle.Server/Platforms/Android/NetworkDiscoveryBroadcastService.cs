using Android.Content;
using Android.Net.Nsd;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace Huddle.Server.Platforms.Android;

internal class NetworkDiscoveryBroadcastService(int broadcastPort, ILogger<NetworkDiscoveryBroadcastService> logger) : IBroadcastService
{
    private NsdServiceInfo? _serviceInfo;
    private RegistrationListener? _registrationListener;

    public bool IsActive { get; private set; }

    public void Dispose()
    {
        _registrationListener?.Dispose();
        _serviceInfo?.Dispose();
    }

    public Task StartAsync(string serviceType, string instanceName)
    {
        _serviceInfo = new NsdServiceInfo()
        {
            ServiceName = instanceName,
            ServiceType = $"_{serviceType}._udp",
            Port = broadcastPort
        };

        //AddAttributes(_serviceInfo, ipAddress, httpPort, queuePort, listeningPort);

        _registrationListener = new RegistrationListener(logger);
        var nsdManager = (NsdManager?)Application.Context.GetSystemService(Context.NsdService);
        if (nsdManager != null)
        {
            nsdManager.RegisterService(_serviceInfo, NsdProtocol.DnsSd, _registrationListener);
            IsActive = true;
        }
        else
        {
            logger.LogError("NetworkDiscoveryBroadcastService: Could not find nsd service");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        var nsdManager = (NsdManager?)Application.Context.GetSystemService(Context.NsdService);
        nsdManager?.UnregisterService(_registrationListener);
        IsActive = false;
        return Task.CompletedTask;
    }

    /*private void AddAttributes(NsdServiceInfo serviceInfo, string ipAddress, int? httpPort, int? queuePort, int? listeningPort)
    {
        var deviceId = deviceInfoProvider.GetDeviceIdentifier() ?? string.Empty;
        if (!string.IsNullOrEmpty(deviceId))
        {
            serviceInfo.SetAttribute("DeviceId", deviceId);
        }
        serviceInfo.SetAttribute("IpAddress", ipAddress);
        if (httpPort.HasValue)
        {
            serviceInfo.SetAttribute("HttpPort", httpPort.ToString());
        }
        if (queuePort.HasValue)
        {
            serviceInfo.SetAttribute("QueuePort", queuePort.ToString());
        }
        if (listeningPort.HasValue)
        {
            serviceInfo.SetAttribute("ListeningPort", listeningPort.ToString());
        }
    }*/
}
