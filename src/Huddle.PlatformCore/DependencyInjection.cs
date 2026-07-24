using Huddle.Core.Services;
using Huddle.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
#if IOS
using Huddle.Core.Platforms.iOS;
#endif
#if ANDROID
using Huddle.Core.Platforms.Android;
#endif
#if WINDOWS
using Huddle.Core.Platforms.Windows;
#endif

namespace Huddle.Core
{
    public static class DependencyInjection
    {
        public static void AddHuddleCore(this IServiceCollection services, bool withMessaging)
        {
            services.AddSingleton<IIpAddressRetrievalService, IpAddressRetrievalService>();

            if (withMessaging)
            {
#if IOS
                services.AddTransient<IMessagingService, NWConnectionMessagingService>();
#else
                services.AddTransient<IMessagingService, UdpMessagingService>();
#endif
            }

#if IOS
            services.AddSingleton<IDeviceInfoProvider, DeviceIdProvider>();
#elif ANDROID
            services.AddSingleton<IDeviceInfoProvider, DeviceIdProvider>();
#elif WINDOWS
            services.AddSingleton<IDeviceInfoProvider, DeviceIdProvider>();
#endif
        }
    }
}
