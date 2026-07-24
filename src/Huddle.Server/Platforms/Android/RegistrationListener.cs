using Android.Net.Nsd;
using Microsoft.Extensions.Logging;

namespace Huddle.Server.Platforms.Android;

internal class RegistrationListener(ILogger logger) : Java.Lang.Object, NsdManager.IRegistrationListener
{
    public void OnServiceRegistered(NsdServiceInfo? serviceInfo)
    {
        logger.LogInformation("NetworkDiscoveryBroadcastService: Started broadcasting as {serviceName}", serviceInfo?.ServiceName);
    }

    public void OnRegistrationFailed(NsdServiceInfo? serviceInfo, NsdFailure errorCode)
    {
        logger.LogError("NetworkDiscoveryBroadcastService: Failed to start broadcasting {errorCode}", errorCode);
    }

    public void OnServiceUnregistered(NsdServiceInfo? serviceInfo)
    {
        logger.LogDebug("RegistrationListener:OnServiceUnregistered {serviceName}", serviceInfo?.ServiceName);
    }

    public void OnUnregistrationFailed(NsdServiceInfo? serviceInfo, NsdFailure errorCode)
    {
        logger.LogDebug("RegistrationListener:OnUnregistrationFailed{errorCode}", errorCode);
    }
}
