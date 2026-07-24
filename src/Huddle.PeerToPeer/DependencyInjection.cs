namespace Huddle.PeerToPeer;

public static class DependencyInjection
{
    public static IHuddlePeerToPeerBuilder AddHuddlePeerToPeer(this IServiceCollection services, string serviceName)
    {
        return new Builder(services, serviceName);
    }

    public static MauiApp StartHuddlePeerToPeerInBackground(this MauiApp app)
    {
        var messagingService = app.Services.GetRequiredService<IMessagingService>();

        _ = Task.Run(async () => await messagingService.StartAsync());

        return app;
    }
}
