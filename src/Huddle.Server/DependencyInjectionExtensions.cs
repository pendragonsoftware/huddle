using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;
#if IOS
using Huddle.Server.Platforms.iOS;
#endif
#if ANDROID
using Huddle.Server.Platforms.Android;
#endif
#if WINDOWS
using Huddle.Server.Platforms.Windows;
#endif

namespace Huddle.Server;

internal static class DependencyInjectionExtensions
{
    private const int DEFAULT_PORT = 5353; // Bonjour port

    internal static void RegisterBroadcastService(this IServiceCollection services, int? broadcastPort)
    {
#if IOS
        services.AddSingleton<IBroadcastService, NWBroadcastService>(sp => new NWBroadcastService(
            broadcastPort ?? DEFAULT_PORT,
            sp.GetRequiredService<ILogger<NWBroadcastService>>()));
#elif ANDROID
        services.AddSingleton<IBroadcastService, NetworkDiscoveryBroadcastService>(sp => new NetworkDiscoveryBroadcastService(
            broadcastPort ?? DEFAULT_PORT,
            sp.GetRequiredService<ILogger<NetworkDiscoveryBroadcastService>>()));
#elif WINDOWS
        services.AddSingleton<IBroadcastService, DnssdBroadcastService>(sp => new DnssdBroadcastService(
            broadcastPort ?? DEFAULT_PORT,
            sp.GetRequiredService<ILogger<DnssdBroadcastService>>()));
#endif
    }
}
