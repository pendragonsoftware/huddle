using Huddle.Server.Builders;
using Huddle.Server.Models;

namespace Huddle.Server.Queue;

public class QueueWithDlq
{
    private readonly List<(QueueContext Context, IQueueHandler Handler)> _messageQueue = [];
    private readonly List<(QueueContext Context, IQueueHandler Handler, Exception Exception)>? _dlq;

    public event EventHandler<Exception>? AddedToDlq;

    internal QueueWithDlq(bool withDlq)
    {
        if (withDlq)
        {
            _dlq = [];
        }
    }

    public bool Dequeue()
    {
        if (_messageQueue.Count > 0)
        {
            var (context, handler) = _messageQueue[0];
            _messageQueue.RemoveAt(0);
            try
            {
                handler.MessageRecieved(context);
            }
            catch (Exception ex)
            {
                if (_dlq != null)
                {
                    _dlq.Add((context, handler, ex));
                    AddedToDlq?.Invoke(this, ex);
                }
                else
                {
                    throw;
                }
            }
            return true;
        }
        return false;
    }

    public void Enqueue(QueueContext context, IQueueHandler handler)
    {
        _messageQueue.Add((context, handler));
    }

    public IEnumerable<QueueContext> PollForMessages(int? limit = null)
    {
        var take = Math.Min(limit ?? _messageQueue.Count, _messageQueue.Count);
        for (var i = 0; i < take; i++)
        {
            yield return _messageQueue[i].Context;
        }
    }

    public bool RemoveFromQueue(QueueContext context)
    {
        var index = _messageQueue.FindIndex(x => x.Context == context);
        if (index >= 0)
        {
            _messageQueue.RemoveAt(index);
            return true;
        }
        return false;
    }

    public IEnumerable<QueueContext> PollDlqForMessages(int? limit = null)
    {
        if (_dlq == null)
        {
            throw new Exception("Queue: No DLQ registered");
        }

        var take = Math.Min(limit ?? _dlq.Count, _dlq.Count);
        for (var i = 0; i < take; i++)
        {
            yield return _dlq[i].Context;
        }
    }

    public bool RemoveFromDlq(QueueContext context)
    {
        if (_dlq == null)
        {
            throw new Exception("Queue: No DLQ registered");
        }

        var index = _dlq.FindIndex(x => x.Context == context);
        if (index >= 0)
        {
            _dlq.RemoveAt(index);
            return true;
        }
        return false;
    }
}
