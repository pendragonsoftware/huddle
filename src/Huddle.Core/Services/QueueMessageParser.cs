namespace Huddle.Core.Services
{
    internal static class QueueMessageParser
    {
        internal static bool TryParse(
            string message,
            out string queueName,
            out string sourceIpAddress,
            out string messageContent)
        {
            queueName = string.Empty;
            sourceIpAddress = string.Empty;
            messageContent = string.Empty;

            var parts = message.Split(':', 3);
            if (parts.Length != 3 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            {
                return false;
            }

            queueName = parts[0];
            sourceIpAddress = parts[1];
            messageContent = parts[2];
            return true;
        }
    }
}
