using System.Text;

namespace Huddle.Core.Services
{
    internal static class InstanceNameFormatter
    {
        internal static string GetIpAddress(string ipAddress)
        {
            var charIp = ipAddress.ToCharArray();

            var convertedIp = charIp
                .Select(x => (int)x >= 48 && (int)x <= 57 ? (char)(x + 17) : x)
                .ToList();

            var sIp = new StringBuilder();
            foreach (var c in convertedIp)
            {
                sIp.Append(c);
            }

            var sIpAddress = sIp.ToString().Replace(".", "_");

            return sIpAddress;
        }

        internal static string GetPort(int? port)
        {
            if (port == null)
            {
                return string.Empty;
            }

            var charPort = port.Value.ToString().ToCharArray();

            var convertedPort = charPort
                .Select(x => x >= 48 && x <= 57 ? (char)(x + 17) : x)
                .ToList();

            var sConvertedPort = new StringBuilder();
            foreach (var c in convertedPort)
            {
                sConvertedPort.Append(c);
            }

            return sConvertedPort.ToString();
        }
    }
}
