using Android.App;
using Android.Runtime;
using MauiWifiManager;

namespace Huddle.Sample.Server.Android;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        WifiNetworkService.Init(this);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
