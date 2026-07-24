namespace Huddle.Core.Services
{
    internal static class QueueMessageFormatter
    {
        internal static string Format(string queueName, string? sourceIpAddress, string message)
        {
            return $"{queueName}:{sourceIpAddress}:{message}";
        }
    }
}
