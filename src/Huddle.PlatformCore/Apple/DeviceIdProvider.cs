// Apple's Network framework, which iOS and Mac Catalyst share. Outside Platforms/ because MAUI compiles only the
// current platform's folder there, and Platforms/iOS is not Mac Catalyst's.
#if IOS || MACCATALYST
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
#endif
