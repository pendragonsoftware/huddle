using Microsoft.Extensions.Logging;
using Huddle.Core.Services.Interfaces;
using Huddle.Core;
using Huddle.Client;
using Huddle.Client.Services.Interfaces;
using Huddle.Server.Services.Interfaces;
using Huddle.Client.Services;
using Huddle.Server;

#if IOS
using Huddle.Client.Platforms.iOS;
using Huddle.Server.Platforms.iOS;
#elif ANDROID
using Huddle.Client.Platforms.Android;
using Huddle.Server.Platforms.Android;
#elif WINDOWS
using Huddle.Client.Platforms.Windows;
using Huddle.Server.Platforms.Windows;
#endif

namespace Huddle.PeerToPeer;

public interface IHuddlePeerToPeerBuilder
{
    IHuddlePeerToPeerBuilder WithDisplayName(string displayName);

    IHuddlePeerToPeerBuilder MapHandler<T>() where T : class, IMessageHandler;

    IServiceCollection Build();
}

public class Builder(IServiceCollection services, string serviceName) : IHuddlePeerToPeerBuilder
{
    private string _displayName = string.Empty;

    public IHuddlePeerToPeerBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    public IHuddlePeerToPeerBuilder MapHandler<T>() where T : class, IMessageHandler
    {
        services.AddSingleton<IMessageHandler, T>();
        return this;
    }

    public IServiceCollection Build()
    {
        services.AddHuddleCore(true);

        services.AddSingleton<IMessagingService, MessagingService>(sp => new MessagingService(
            serviceName,
            _displayName,
            sp,
            sp.GetRequiredService<IIpAddressRetrievalService>(),
            sp.GetRequiredService<IBroadcastService>(),
            sp.GetRequiredService<IDiscoveryService>(),
            sp.GetRequiredService<Core.Services.Interfaces.IMessagingService>(),
            sp.GetRequiredService<ILogger<MessagingService>>()));

        services.RegisterBroadcastService(null);

        services.AddSingleton<IServerDiscoveryService>(sp => new ServerDiscoveryService(
            true,
            sp.GetRequiredService<IDiscoveryService>(),
            sp.GetRequiredService<Core.Services.Interfaces.IMessagingService>(),
            sp.GetRequiredService<IIpAddressRetrievalService>(),
            sp.GetRequiredService<IDeviceInfoProvider>(),
            sp.GetRequiredService<ILogger<ServerDiscoveryService>>()));

#if IOS
        ObjCRuntime.Class.ThrowOnInitFailure = false;
        services.AddSingleton<IDiscoveryService>(sp => new NWBrowserDiscoveryService(
            serviceName,
            sp.GetRequiredService<ILogger<NWBrowserDiscoveryService>>()));
#elif ANDROID
        services.AddSingleton<IDiscoveryService>(sp => new NetworkDiscoveryService(
            serviceName,
            sp.GetRequiredService<ILogger<NetworkDiscoveryService>>()));
#elif WINDOWS
        services.AddSingleton<IDiscoveryService>(sp => new DnssdDiscoveryService(
            serviceName,
            sp.GetRequiredService<ILogger<DnssdDiscoveryService>>()));
#else

#endif
        return services;
    }
}
