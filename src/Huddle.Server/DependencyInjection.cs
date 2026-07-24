using Huddle.Server.Builders;
using Huddle.Server.Implementations;
using Microsoft.Extensions.Logging;

namespace Huddle.Server;

public static class DependencyInjection
{
    public static IServerBuilder AddHuddle(this IServiceCollection services, string serviceName)
    {
        return new ServerBuilder(services, serviceName);
    }

    public static MauiApp StartHuddle(this MauiApp app)
    {
        // Instantiate this first so it registers event handlers and starts running (as singleton)
        var server = app.Services.GetRequiredService<IMobileServer>();

        Task.Run(async () =>
        {
            try
            {
                await server.StartAsync();
            }
            catch (Exception ex)
            {
                await server.StopAsync();

                var logger = app.Services.GetService<ILogger<HuddleInstance>>();
                logger?.LogError(ex, "StartHuddle: Error starting server");
            }
        });

        return app;
    }
}
