using Huddle.Core.Services.Interfaces;
using Application = Android.App.Application;
using Secure = Android.Provider.Settings.Secure;

namespace Huddle.Core.Platforms.Android
{
    public class DeviceIdProvider : IDeviceInfoProvider
    {
        public string? GetDeviceIdentifier()
        {
            var context = Application.Context;

            if (context.ContentResolver != null)
            {
                var id = Secure.GetString(context.ContentResolver, Secure.AndroidId);
                return id;
            }

            return null;
        }
    }
}