namespace Huddle.Sample.PeerToPeer;

public class MessageBus
{
    public event EventHandler<(string Message, string DisplayName)>? MessageReceived;

    public void PostMessage(string message, string displayName)
    {
        MessageReceived?.Invoke(this, (message, displayName));
    }
}
