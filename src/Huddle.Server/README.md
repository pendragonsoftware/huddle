# Huddle.Server

`Huddle.Server` turns a .NET MAUI app into a discoverable local server. It can advertise itself on the local network, host a small HTTP API, receive direct client messages, process queue messages, and send messages back to connected clients.

## Install

```bash
dotnet add package Huddle.Server
```

## Register A Server

Register the server in `MauiProgram.cs`. The service name is the discovery name clients will search for, so use the same value in the client app.

```csharp
using Huddle.Server;
using Huddle.Server.Builders;
using Huddle.Server.Models;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        builder.Services.AddHuddle("huddle")
            .AddMessaging()
                .MapHandler<ClientMessageHandler>()
            .AddQueue("jobs")
                .WithDlq()
                .MapHandler<JobQueueHandler>()
                .Server
            .AddHttpApi()
                .WithPort(11000)
                .MapGet("/status", _ => Task.FromResult(Results.Ok("online")))
                .MapPost("/echo", context =>
                {
                    var body = context.Request.Body;

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        return Task.FromResult(Results.BadRequest());
                    }

                    return Task.FromResult(Results.Ok($"Received '{body}' from {context.SourceHost}"));
                })
                .Server
            .Build();

        return builder
            .Build()
            .StartHuddle();
    }
}
```

## Direct Client Messages

Map an `IMessageHandler` to handle messages sent by connected clients.

```csharp
using Huddle.Server;

public sealed class ClientMessageHandler : IMessageHandler
{
    public Task HandleMessageAsync(string message, string peerId)
    {
        Console.WriteLine($"Client {peerId}: {message}");
        return Task.CompletedTask;
    }
}
```

After a client connects, the server can send messages back to all connected clients through `IMobileServer`.

```csharp
using Huddle.Server;

public sealed class ServerStatusViewModel(IMobileServer server)
{
    public Task SendToClientsAsync(string message)
    {
        return server.SendMessageToClientsAsync(message);
    }
}
```

## Queue Messages

Add queues with `.AddQueue("queue-name")`. Use `.WithDlq()` to enable dead-letter queue support when handling fails.

```csharp
using Huddle.Server;
using Huddle.Server.Models;
using Huddle.Server.Builders;

public sealed class JobQueueHandler : IQueueHandler
{
    public void MessageRecieved(QueueContext context)
    {
        Console.WriteLine($"Queued message from {context.SourceHost}: {context.Message}");
    }

    public void MessageHandlingError(QueueContext context, Exception exception, bool addedToDlq)
    {
        Console.WriteLine($"Queue error. Added to DLQ: {addedToDlq}. {exception.Message}");
    }
}
```

## HTTP API

Use `.AddHttpApi()` to expose local endpoints from the MAUI app. The server advertises the actual port during discovery so clients can point their typed `HttpClient` instances at it.

`.WithPort(...)` is a preference, not a guarantee: if the requested port is unavailable the server falls back to an automatically assigned one (clients still find it through discovery). Omitting `.WithPort(...)` picks a free port on first launch and reuses it on later launches, keeping Windows URL ACL reservations and firewall rules stable. Use `.WithPort(...)` when the port must be known in advance, e.g. for firewall allow-listing.

```csharp
using Huddle.Server.Models;

builder.Services.AddHuddle("huddle")
    .AddHttpApi()
        .WithPort(11000)
        .MapGet("/status", _ => Task.FromResult(Results.Ok("online")))
        .MapPost("/echo", context =>
        {
            if (string.IsNullOrWhiteSpace(context.Request.Body))
            {
                return Task.FromResult(Results.BadRequest());
            }

            return Task.FromResult(Results.Ok(context.Request.Body));
        })
        .Server
    .Build();
```

## Streaming Request Bodies (Large Uploads)

By default a route's body is read into `context.Request.Body` as a string. For large binary payloads (photos, videos, multi-GB files) map the route with `HttpBodyMode.Streamed` instead: the handler gets the raw bytes as `context.Request.BodyStream` with no buffering anywhere in the pipeline - memory use is O(buffer) regardless of payload size, so clients can send raw bytes instead of a base64 JSON envelope.

```csharp
using Huddle.Server.Models;
using System.Security.Cryptography;

builder.Services.AddHuddle("huddle")
    .AddHttpApi()
        .MapPost("/upload", new HttpEndpointOptions { BodyMode = HttpBodyMode.Streamed, MaxBodyBytes = 4L * 1024 * 1024 * 1024 }, async context =>
        {
            // Upload metadata travels in the query string and headers.
            var fileName = Path.GetFileName(context.Request.QueryString["fileName"]);
            var targetPath = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var file = File.Create(targetPath);
            try
            {
                // Stream straight to disk, hashing incrementally as bytes arrive.
                var buffer = new byte[81920];
                int read;
                while ((read = await context.Request.BodyStream!.ReadAsync(buffer)) > 0)
                {
                    sha256.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read));
                }
            }
            catch (RequestAbortedException)
            {
                // The client disconnected mid-upload - clean up the half-written file.
                await file.DisposeAsync();
                File.Delete(targetPath);
                throw;
            }
            await file.DisposeAsync();

            return Results.Ok(Convert.ToHexString(sha256.GetHashAndReset()));
        })
        .Server
    .Build();
```

Behaviour of `BodyStream`:

- Read-once and forward-only; on streamed routes `context.Request.Body` is `null`.
- Both `Content-Length` and chunked bodies are supported. `context.Request.ContentLength` reports the declared length, or `null` when the body is chunked.
- A read returns `0` when the body has ended cleanly. If the client disconnects mid-upload the read throws `RequestAbortedException` instead, so "body ended" and "client aborted" are always distinguishable.
- `context.Aborted` is a `CancellationToken` that fires when the server is stopping - pass it to long-running work such as `CopyToAsync`.

Body size limits are opt-in, per route via `HttpEndpointOptions.MaxBodyBytes` or server-wide via `.WithMaxRequestBodyBytes(...)` (a route's own value wins). Requests declaring a larger `Content-Length` are rejected with `413 Payload Too Large` before the handler runs; chunked bodies are cut off with a 413 as soon as they cross the limit. Limits also apply to string-body routes.

## Inspect The Running Server

Inject `IMobileServer`, `IHttpApi`, or `IQueue` if you want to inspect or control the running server.

```csharp
using Huddle.Server;

public sealed class ServerStatusViewModel(IMobileServer server)
{
    public string? IpAddress => server.IpAddress;
    public int? HttpPort => server.HttpApi?.Port;
    public int? QueuePort => server.Queue?.Port;
}
```

## Sample

The server sample is in `samples/Huddle.Sample.Server`.

```bash
dotnet build samples/Huddle.Sample.Server.sln
```

## License And Attribution

Huddle.Server is open source under the Apache License 2.0.

Attribution to Pendragon Development is appreciated. If this package helps your project, a mention of Pendragon Development or a link back to the Huddle repository is welcome, but not required by the license.
