namespace Huddle.Server.Models;

public record QueueContext(IServiceProvider ServiceProvider, string SourceHost, string Message);
