using Huddle.Server.Models;

namespace Huddle.Server.Builders;

public interface IQueueHandler
{
    void MessageRecieved(QueueContext context);
    void MessageHandlingError(QueueContext context, Exception exception, bool addedToDlq);
}

public interface IQueueBuilder
{
    IServerBuilder Server { get; }
    IQueueBuilder WithPort(int port);
    IQueueBuilder WithDlq();
    IQueueBuilder MapHandler<T>() where T : class, IQueueHandler;
}

internal class QueueBuilder(ServerBuilder serverBuilder, IServiceCollection services, string name) : IQueueBuilder
{
    private readonly Dictionary<string, Type> _handlers = [];

    private int _port = 0;
    private bool _dlq = false;

    internal int Port => _port;
    internal bool Dlq => _dlq;
    internal Dictionary<string, Type>Handlers => _handlers;

    public IServerBuilder Server => serverBuilder;

    public IQueueBuilder WithPort(int port)
    {
        _port = port;
        return this;
    }

    public IQueueBuilder WithDlq()
    {
        _dlq = true;
        return this;
    }

    public IQueueBuilder MapHandler<T>() where T : class, IQueueHandler
    {
        services.AddSingleton<T>();
        _handlers.Add(name, typeof(T));
        return this;
    }
}
