namespace Huddle.Core.Services
{
    internal static class ConnectionMessageFormatter
    {
        internal static string FormatRequest(string? deviceId, string? ipAddress, int listeningPort)
        {
            return $"{Constants.CONNECTION_MESSAGE_PREFIX}{deviceId}:{ipAddress}:{listeningPort}";
        }

        internal static string FormatConfirmation(string deviceId)
        {
            return $"{Constants.CONNECTION_MESSAGE_PREFIX}{deviceId}";
        }
    }
}
