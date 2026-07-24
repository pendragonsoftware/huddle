namespace Huddle.Core.Services
{
    internal static class ConnectionMessageParser
    {
        internal static bool TryParseRequest(
            string message,
            out string deviceId,
            out string ipAddress,
            out int listeningPort)
        {
            deviceId = string.Empty;
            ipAddress = string.Empty;
            listeningPort = 0;

            if (!message.StartsWith(Constants.CONNECTION_MESSAGE_PREFIX))
            {
                return false;
            }

            var payload = message[Constants.CONNECTION_MESSAGE_PREFIX.Length..];
            var parts = payload.Split(':', 3);
            if (parts.Length != 3 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            {
                return false;
            }

            if (!int.TryParse(parts[2], out var parsedPort) || parsedPort <= 0)
            {
                return false;
            }

            deviceId = parts[0];
            ipAddress = parts[1];
            listeningPort = parsedPort;
            return true;
        }

        internal static bool TryParseConfirmation(string message, out string deviceId)
        {
            deviceId = string.Empty;

            if (!message.StartsWith(Constants.CONNECTION_MESSAGE_PREFIX))
            {
                return false;
            }

            deviceId = message[Constants.CONNECTION_MESSAGE_PREFIX.Length..];
            return !string.IsNullOrEmpty(deviceId);
        }
    }
}
