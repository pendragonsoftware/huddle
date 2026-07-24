using Huddle.Client.Services;

namespace Huddle.Client
{
    public static class DependencyInjection
    {
        public static IHuddleBuilder AddHuddle(this IServiceCollection services, string serviceName)
        {
            return new Builder(services, serviceName);
        }

        public static void StartHuddleDiscoveryInBackground(this MauiApp app)
        {
            // Instantiate this first so it registers event handlers and starts running (as singleton)
            app.Services.GetRequiredService<ServerClientSyncingService>();
            var serverDiscoveryService = app.Services.GetRequiredService<IServerDiscoveryService>()
                ?? throw new Exception("You must use one of the Add methods on the return IHuddleBuilder from AddHuddle (followed by Build) to use this method");

            serverDiscoveryService.SearchContinuously();
        }
    }
}
