namespace Huddle.PeerToPeer;

public interface IMessageHandler
{
    Task HandleMessageAsync(string message, string displayName);
}