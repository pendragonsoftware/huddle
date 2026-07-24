namespace Huddle.PeerToPeer.Services;

internal static class InstanceNameHelper
{
    public static bool TryParse(string? value, out string displayName, out string ipAddress, out int port)
    {
        displayName = string.Empty;
        ipAddress = string.Empty;
        port = 0;

        if (value == null)
        {
            return false;
        }

        try
        {
            var split = value.Split(':');
            if (split.Length != 3)
            {
                return false;
            }

            displayName = split[0];

            if (!Core.Services.InstanceNameParser.TryGetIpAddress(split[1], out ipAddress))
            {
                return false;
            }

            if (!Core.Services.InstanceNameParser.TryGetPort(split[2], out var iPort) || iPort == null || iPort <= 0)
            {
                return false;
            }
            port = iPort.Value;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string Format(string displayName, string ipAddress, int port)
    {
        var sIpAddress = Core.Services.InstanceNameFormatter.GetIpAddress(ipAddress);
        var sPort = Core.Services.InstanceNameFormatter.GetPort(port);

        return $"{displayName}:{sIpAddress}:{sPort}";
    }
}
