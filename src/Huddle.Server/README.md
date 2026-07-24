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

Use `.AddHttpApi()` to expose local endpoints from the MAUI app. The server advertises the configured port during discovery so clients can point their typed `HttpClient` instances at it.

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
