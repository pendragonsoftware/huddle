using Android.Net.Nsd;
using Microsoft.Extensions.Logging;
using static Android.Net.Nsd.NsdManager;

namespace Huddle.Client.Platforms.Android
{
    public class ResolveListener(ILogger logger) : Java.Lang.Object, IResolveListener
    {
        public void OnServiceResolved(NsdServiceInfo? serviceInfo)
        {
            logger.LogDebug("NetworkDiscoveryService: Resolve {@serviceName}", serviceInfo);
        }

        public void OnResolveFailed(NsdServiceInfo? serviceInfo, NsdFailure errorCode)
        {
            logger.LogDebug("NetworkDiscoveryService: Failed to resolve {serviceName} with {errorCode}", serviceInfo?.ServiceName, errorCode);
        }
    }
}
