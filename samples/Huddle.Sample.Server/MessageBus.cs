namespace Huddle.Sample.Server;

public class MessageBus
{
    public event EventHandler<string>? ApiMessageReceived;
    public event EventHandler<string>? QueueMessageReceived;
    public event EventHandler<string>? QueueMessageError;
    public event EventHandler<string>? MessageReceived;

    public void PostApiMessage(string message)
    {
        ApiMessageReceived?.Invoke(this, message);
    }

    public void PostMessage(string message)
    {
        MessageReceived?.Invoke(this, message);
    }

    public void PostQueueMessage(string message)
    {
        QueueMessageReceived?.Invoke(this, message);
    }

    public void PostQueueError(string message)
    {
        QueueMessageError?.Invoke(this, message);
    }
}
