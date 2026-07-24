using Huddle.Core;
using Huddle.Core.Services.Interfaces;
using Huddle.Server.Implementations;
using Huddle.Server.Services;
using Huddle.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Huddle.Server.Builders;

public interface IServerBuilder
{
    IHttpApiBuilder AddHttpApi();

    IQueueBuilder AddQueue(string queueName);

    IMessagingBuilder AddMessaging();

    IServiceCollection Build();
}

internal class ServerBuilder : IServerBuilder
{
    private readonly IServiceCollection _services;
    private readonly string _serviceName = string.Empty;

    private HttpApiBuilder? _apiBuilder;
    private QueueBuilder? _queueBuilder;
    private bool _withMessaging = false;

    internal ServerBuilder(IServiceCollection services, string serviceName)
    {
        _services = services;
        _serviceName = serviceName;
    }

    public IHttpApiBuilder AddHttpApi()
    {
        _services.AddSingleton<INetworkListenerService, HttpListenerService>();

        _apiBuilder = new HttpApiBuilder(this);
        return _apiBuilder;
    }

    public IQueueBuilder AddQueue(string queueName)
    {
        _services.AddSingleton<INetworkQueueService, NetworkQueueService>();

        _queueBuilder = new QueueBuilder(this, _services, queueName);
        return _queueBuilder;
    }

    public IMessagingBuilder AddMessaging()
    {
        _withMessaging = true;

        return new MessagingBuilder(this, _services);
    }

    public IServiceCollection Build()
    {
        _services.AddHuddleCore(_withMessaging);

        if (_withMessaging)
        {
            _services.AddSingleton<ServerClientMessagingService>();
        }

        _services.RegisterBroadcastService(null);

        _services.AddSingleton<IMobileServer>(sp =>
        {
            return new HuddleInstance(
                sp,
                sp.GetRequiredService<IIpAddressRetrievalService>(),
                sp.GetRequiredService<IDeviceInfoProvider>(),
                sp.GetRequiredService<IBroadcastService>(),
                sp.GetService<ServerClientMessagingService>(),
                sp.GetRequiredService<INetworkListenerService>(),
                sp.GetService<INetworkQueueService>(),
                sp.GetRequiredService<ILogger<HuddleQueue>>(),
                _serviceName,
                _apiBuilder?.Port,
                _queueBuilder?.Port,
                _queueBuilder?.Dlq ?? false,
                _apiBuilder?.Endpoints ?? [],
                _queueBuilder?.Handlers ?? []);
        });

        _services.AddSingleton<IHttpApi>(sp =>
        {
            var server = sp.GetRequiredService<IMobileServer>();
            if (server.HttpApi == null)
            {
                throw new Exception("Not HttpApi registered with server");
            }
            return server.HttpApi;
        });

        _services.AddSingleton<IQueue>(sp =>
        {
            var server = sp.GetRequiredService<IMobileServer>();
            if (server.Queue == null)
            {
                throw new Exception("Not Queue registered with server");
            }
            return server.Queue;
        });

        return _services;
    }
}
