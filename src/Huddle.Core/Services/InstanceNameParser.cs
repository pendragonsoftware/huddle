using System.Text;

namespace Huddle.Core.Services
{
    internal static class InstanceNameParser
    {
        internal static bool TryParseAttributes(
            IDictionary<string, string> attributes,
            out string ipAddress,
            out string deviceId,
            out int? httpPort,
            out int? queuePort,
            out int? listeningPort)
        {
            ipAddress = string.Empty;
            deviceId = string.Empty;
            httpPort = 0;
            queuePort = 0;
            listeningPort = 0;

            try
            {
                if (attributes.TryGetValue("IpAddress", out var sIpAddress))
                {
                    ipAddress = sIpAddress;
                }
                else
                {
                    return false;
                }

                if (attributes.TryGetValue("DeviceId", out var sDeviceId))
                {
                    deviceId = sDeviceId;
                }
                else
                {
                    return false;
                }

                if (attributes.TryGetValue("HttpPort", out var sServerPort) && int.TryParse(sServerPort, out var iServerPort))
                {
                    httpPort = iServerPort;
                }
                else
                {
                    return false;
                }

                if (attributes.TryGetValue("QueuePort", out var sQueuePort) && int.TryParse(sQueuePort, out var iQueuePort))
                {
                    queuePort = iQueuePort;
                }
                else
                {
                    return false;
                }

                if (attributes.TryGetValue("ListeningPort", out var sListeningPort) && int.TryParse(sListeningPort, out var iListeningPort))
                {
                    listeningPort = iListeningPort;
                }
                else
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

        internal static bool TryGetIpAddress(string s, out string ipAddress)
        {
            try
            {
                if (!s.Contains("_"))
                {
                    ipAddress = string.Empty;
                    return false;
                }
                var ip = s.Replace("_", ".");

                var charIp = ip.ToCharArray();

                var convertedIp = charIp
                    .Select(x => (int)x >= 65 && (int)x <= 75 ? (char)(x - 17) : x)
                    .ToList();

                var sIp = new StringBuilder();
                foreach (var c in convertedIp)
                {
                    sIp.Append(c);
                }
                ipAddress = sIp.ToString();
                return true;
            }
            catch (Exception)
            {
                ipAddress = string.Empty;
                return false;
            }
        }

        internal static bool TryGetPort(string s, out int? port)
        {
            if (string.IsNullOrEmpty(s))
            {
                port = null;
                return true;
            }

            try
            {
                var charPort = s
                   .ToString()
                   .ToCharArray();

                var convertedPort = charPort
                    .Select(x => (int)x >= 65 && (int)x <= 75 ? (char)(x - 17) : x)
                    .ToList();

                var sConvertedPort = new StringBuilder();
                foreach (var c in convertedPort)
                {
                    sConvertedPort.Append(c);
                }

                if (!int.TryParse(sConvertedPort.ToString(), out var iPort))
                {
                    port = null;
                    return false;
                }

                port = iPort;
                return true;
            }
            catch (Exception)
            {
                port = null;
                return false;
            }
        }
    }
}
