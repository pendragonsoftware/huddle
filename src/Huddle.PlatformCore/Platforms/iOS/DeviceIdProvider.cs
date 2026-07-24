using Huddle.Core.Services.Interfaces;
using UIKit;

namespace Huddle.Core.Platforms.iOS;

public class DeviceIdProvider : IDeviceInfoProvider
{
    public string? GetDeviceIdentifier()
    {
        return UIDevice.CurrentDevice.IdentifierForVendor?.AsString()?.Replace("-", "");
    }
}
