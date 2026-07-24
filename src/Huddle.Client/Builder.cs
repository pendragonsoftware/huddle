using Huddle.Client.Services;
using Microsoft.Extensions.Logging;
using Huddle.Core.Services.Interfaces;
using Huddle.Core;
using Huddle.Client.Services.Interfaces;
#if IOS
using Huddle.Client.Platforms.iOS;
#elif ANDROID
using Huddle.Client.Platforms.Android;
#elif WINDOWS
using Huddle.Client.Platforms.Windows;
#endif

namespace Huddle.Client;

public interface IHuddleBuilder
{
    IHuddleBuilder WithMessaging(bool withMessaging);

    IHttpClientBuilder AddHttpClient<T>(bool updateHttpClientsOnDiscovery = true) where T : class;

    IHuddleBuilder AddQueueClient<T>(bool updateQueueClientsOnDiscovery = true) where T : class;

    IServiceCollection Build();
}

public class Builder(IServiceCollection services, string serviceName) : IHuddleBuilder
{
    private bool _withMessaging = false;

    public IHuddleBuilder WithMessaging(bool withMessaging)
    {
        _withMessaging = withMessaging;
        return this;
    }

    public IHttpClientBuilder AddHttpClient<T>(bool updateHttpClientsOnDiscovery = true) where T : class
    {
        var httpClientBuilder = services.AddHttpClient<T>((sp, h) =>
        {
            h.Timeout = TimeSpan.FromSeconds(5);
            if (updateHttpClientsOnDiscovery)
            {
                var syncingService = sp.GetRequiredService<ServerClientSyncingService>();
                syncingService.RegisterHttpClient(h);
            }
        });

        return httpClientBuilder;
    }

    public IHuddleBuilder AddQueueClient<T>(bool updateQueueClientsOnDiscovery = true) where T : class
    {
        services.AddTransient<QueueClient>();
        services.AddTransient<T>(sp =>
        {
            var queueClient = new QueueClient(sp.GetRequiredService<IMessagingService>(), sp.GetRequiredService<IIpAddressRetrievalService>(), sp.GetRequiredService<ILogger<QueueClient>>());

            if (updateQueueClientsOnDiscovery)
            {
                var syncingService = sp.GetRequiredService<ServerClientSyncingService>();
                syncingService.RegisterQueueClient(queueClient);
            }

            return sp.ConstructItemWithRequiredConstructorParameter<T, QueueClient>(queueClient);
        });
        return this;
    }

    public IServiceCollection Build()
    {
        _ = serviceName;

        services.AddHuddleCore(_withMessaging);

        services.AddSingleton<IServerDiscoveryService>(sp => new ServerDiscoveryService(
            _withMessaging,
            sp.GetRequiredService<IDiscoveryService>(),
            sp.GetRequiredService<IMessagingService>(),
            sp.GetRequiredService<IIpAddressRetrievalService>(),
            sp.GetRequiredService<IDeviceInfoProvider>(),
            sp.GetRequiredService<ILogger<ServerDiscoveryService>>()));

        services.AddSingleton<ServerClientSyncingService>();

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
