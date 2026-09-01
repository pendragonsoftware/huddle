using Huddle.Server;
using Huddle.Server.Models;
using MetroLog.MicrosoftExtensions;
using MetroLog.Operators;

namespace Huddle.Sample.Server;

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

        builder.Services.AddHuddle("huddle")
            .AddMessaging()
                .MapHandler<MessageHandler>()
            .AddQueue("scanpost")
                .WithDlq()
                .MapHandler<QueueHandler>()
                .Server
            .AddHttpApi()
                .WithPort(11_000)
                .MapGet("/status", _ => Task.FromResult(Results.Ok()))
                .MapPost("/message", context =>
                {
                    if (context.Request.Body == null)
                    {
                        return Task.FromResult(Results.BadRequest());
                    }
               
                    var message = context.Request.Body;

                    var messageBus = context.ServiceProvider.GetRequiredService<MessageBus>();
                    messageBus.PostApiMessage($"{message} from {context.SourceHost}");

                    return Task.FromResult(Results.Ok($"Received message {message} from {context.SourceHost}"));
                })
                .MapPost("/upload", new HttpEndpointOptions { BodyMode = HttpBodyMode.Streamed, MaxBodyBytes = 4L * 1024 * 1024 * 1024 }, async context =>
                {
                    var fileName = Path.GetFileName(context.Request.QueryString.GetValueOrDefault("fileName", "upload.bin"));
                    var targetPath = Path.Combine(FileSystem.CacheDirectory, fileName);

                    var file = File.Create(targetPath);
                    try
                    {
                        // The body streams straight to disk - memory use stays at O(buffer)
                        // no matter how large the upload is.
                        await context.Request.BodyStream!.CopyToAsync(file, context.Aborted);
                    }
                    catch (RequestAbortedException)
                    {
                        // The client disconnected mid-upload - remove the half-written file.
                        await file.DisposeAsync();
                        File.Delete(targetPath);
                        throw;
                    }
                    await file.DisposeAsync();

                    var messageBus = context.ServiceProvider.GetRequiredService<MessageBus>();
                    messageBus.PostApiMessage($"Received {fileName} ({context.Request.ContentLength?.ToString() ?? "unknown length"} bytes) from {context.SourceHost}");

                    return Results.Ok($"Saved {fileName}");
                })
                .MapPost("/loadtest/start", context =>
                {
                    var amountExpected = context.Request.QueryString["amountExpected"];
                    var messageBus = context.ServiceProvider.GetRequiredService<MessageBus>();
                    LoadTestHandler.StartLoadTest(messageBus, amountExpected);
                    return Task.FromResult(Results.Ok());
                })
                .MapPost("/loadtest/continue", _ =>
                {
                    LoadTestHandler.ContinueLoadTest();
                    return Task.FromResult(Results.Ok());
                })
                .MapPost("/loadtest/end", context =>
                {
                    var messageBus = context.ServiceProvider.GetRequiredService<MessageBus>();
                    var results = LoadTestHandler.EndLoadTest(messageBus);
                    return Task.FromResult(Results.Ok(results));
                })
                .Server
            .Build();


        var mauiApp = builder
            .Build()
            .StartHuddle();

        return mauiApp;
    }
}
