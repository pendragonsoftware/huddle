namespace Huddle.Client.Services
{
    internal static class InstanceNameHelper
    {
        public static bool TryParse(string? value, out string ipAddress, out int? httpPort, out int? queuePort, out int? listeningPort)
        {
            ipAddress = string.Empty;
            httpPort = null;
            queuePort = null;
            listeningPort = null;

            if (value == null)
            {
                return false;
            }

            try
            {
                var split = value.Split(':');
                if (split.Length != 4)
                {
                    return false;
                }

                if (!Core.Services.InstanceNameParser.TryGetIpAddress(split[0], out ipAddress))
                {
                    return false;
                }

                if (!Core.Services.InstanceNameParser.TryGetPort(split[1], out httpPort))
                {
                    return false;
                }

                if (!Core.Services.InstanceNameParser.TryGetPort(split[2], out queuePort))
                {
                    return false;
                }

                if (!Core.Services.InstanceNameParser.TryGetPort(split[3], out listeningPort))
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
