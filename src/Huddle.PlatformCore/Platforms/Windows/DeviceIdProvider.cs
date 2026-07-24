using Huddle.Core.Services.Interfaces;
using Windows.Security.Cryptography;
using Windows.System.Profile;

namespace Huddle.Core.Platforms.Windows
{
    public class DeviceIdProvider : IDeviceInfoProvider
    {
        public string GetDeviceIdentifier()
        {
            var info = SystemIdentification.GetSystemIdForPublisher();

            var asHex = CryptographicBuffer.EncodeToHexString(info.Id);

            return asHex;
        }
    }
}