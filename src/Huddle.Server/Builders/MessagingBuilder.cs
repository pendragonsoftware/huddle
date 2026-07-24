namespace Huddle.Server.Builders;

public interface IMessagingBuilder
{
    IServerBuilder MapHandler<T>() where T : class, IMessageHandler;

    IServerBuilder Server { get; }
}

internal class MessagingBuilder(ServerBuilder serverBuilder, IServiceCollection services) : IMessagingBuilder
{
    public IServerBuilder Server => serverBuilder;

    public IServerBuilder MapHandler<T>() where T : class, IMessageHandler
    {
        services.AddSingleton<IMessageHandler, T>();
        return serverBuilder;
    }
}
