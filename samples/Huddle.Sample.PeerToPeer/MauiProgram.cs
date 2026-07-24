using Huddle.PeerToPeer;
using MetroLog.MicrosoftExtensions;
using MetroLog.Operators;

namespace Huddle.Sample.PeerToPeer;

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

        var messageBus = new MessageBus();
        builder.Services.AddSingleton<MessageBus>(_ => messageBus);

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

        builder.Services.AddSingleton(LogOperatorRetriever.Instance);

        builder.Services.AddSingleton<MainPage>();

        builder.Services.AddHuddlePeerToPeer("huddlepeertopeer")
                .WithDisplayName($"test-{Guid.NewGuid().ToString("N").Substring(0, 6)}")
                .MapHandler<MessageHandler>()
            .Build();

        var mauiApp = builder
            .Build()
            .StartHuddlePeerToPeerInBackground();

        return mauiApp;
    }
}
