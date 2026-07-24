namespace Huddle.Sample.PeerToPeer;

public class MessageHandler(MessageBus messageBus) : Huddle.PeerToPeer.IMessageHandler
{
    public Task HandleMessageAsync(string message, string displayName)
    {
        messageBus.PostMessage(message, displayName);
        return Task.CompletedTask;
    }
}
