namespace Huddle.Server;

public interface IMessageHandler
{
    Task HandleMessageAsync(string message, string peerId);
}