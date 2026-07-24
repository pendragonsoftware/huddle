using Huddle.Server;

namespace Huddle.Sample.Server;

public class MessageHandler(MessageBus messageBus) : IMessageHandler
{
    public Task HandleMessageAsync(string message, string peerId)
    {
        messageBus.PostMessage(message);
        return Task.CompletedTask;
    }
}
