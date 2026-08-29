namespace Huddle.Server.Models;

/// <param name="ServiceProvider">The host app's service provider, for resolving services inside handlers.</param>
/// <param name="SourceHost">The sender's IP address as reported by the sender itself - queue
/// datagrams carry it in their payload (&lt;queue name&gt;:&lt;source ip&gt;:&lt;message&gt;), so unlike
/// <see cref="RequestContext.SourceHost"/> it is not verified against the transport.</param>
/// <param name="Message">The queued message content.</param>
public record QueueContext(IServiceProvider ServiceProvider, string SourceHost, string Message);
