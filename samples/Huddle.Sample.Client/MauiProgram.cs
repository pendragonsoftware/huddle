using Huddle.Sample.Client.Services;
using Huddle.Client;
using MetroLog.MicrosoftExtensions;

namespace Huddle.Sample.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Logging
            .AddTraceLogger(_ => { })
            .AddInMemoryLogger(_ => { })
            .AddConsoleLogger(_ => { })
            .AddStreamingFileLogger(
                options =>
                {
                    options.RetainDays = 2;
                    options.FolderPath = Path.Combine(FileSystem.AppDataDirectory, "MetroLogs");
                });

        builder.Services.AddSingleton<MainPage>();

        var serverBuilder = builder.Services.AddHuddle("huddle")
            .WithMessaging(true);
        serverBuilder
            .AddHttpClient<ServerApiClient>();
        serverBuilder
            .AddQueueClient<ServerQueueClient>();
        serverBuilder.Build();

        var mauiApp = builder.Build();

        mauiApp.StartHuddleDiscoveryInBackground();

        return mauiApp;
    }
}
