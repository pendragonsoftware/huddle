using Huddle.Server.Builders;
using Huddle.Server.Models;

namespace Huddle.Sample.Server;

public class QueueHandler(MessageBus messageBus) : IQueueHandler
{
    public void MessageRecieved(QueueContext context)
    {
        messageBus.PostQueueMessage(context.Message);
    }

    public void MessageHandlingError(QueueContext context, Exception exception, bool addedToDlq)
    {
        messageBus.PostQueueError($"{context.Message}:{exception.Message}");
    }
}
