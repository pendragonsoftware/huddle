# Huddle.Client

`Huddle.Client` lets a .NET MAUI app discover a `Huddle.Server` app on the local network, connect to it, call its HTTP API, send queue messages, and receive direct server messages.

## Install

```bash
dotnet add package Huddle.Client
```

## Register Discovery And Clients

Register discovery, an HTTP API client, and a queue client in `MauiProgram.cs`:

```csharp
using Huddle.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        var mauiServer = builder.Services
            .AddHuddle("huddle")
            .WithMessaging(true);

        mauiServer.AddHttpClient<MyServerApiClient>();
        mauiServer.AddQueueClient<MyQueueClient>();
        mauiServer.Build();

        var app = builder.Build();

        app.StartHuddleDiscoveryInBackground();

        return app;
    }
}
```

The service name must match the value used by the server app.

## Typed HTTP Clients

Create a typed HTTP client. Huddle updates the `HttpClient.BaseAddress` when a server is discovered.

```csharp
using System.Net.Http.Json;

public sealed class MyServerApiClient(HttpClient httpClient)
{
    public Task<HttpResponseMessage> CheckStatusAsync()
    {
        return httpClient.GetAsync("status");
    }

    public Task<HttpResponseMessage> SendEchoAsync(string message)
    {
        return httpClient.PostAsJsonAsync("echo", message);
    }
}
```

## Queue Clients

Create a typed queue client for sending messages to server queues.

```csharp
using Huddle.Client;

public sealed class MyQueueClient(QueueClient queueClient)
{
    public Task<bool> SendJobAsync(string message)
    {
        return queueClient.SendMessageAsync("jobs", message);
    }
}
```

## Discovery And Messaging

Use `IServerDiscoveryService` to observe discovery, connect to a server, and send direct messages:

```csharp
using Huddle.Client;

public sealed class ServerBrowser
{
    private readonly IServerDiscoveryService _discovery;

    public ServerBrowser(IServerDiscoveryService discovery)
    {
        _discovery = discovery;
        _discovery.ServerDiscovered += OnServerDiscovered;
        _discovery.ServerConnectionConfirmed += OnConnected;
    }

    public Task SearchOnceAsync()
    {
        return _discovery.SearchAsync(TimeSpan.FromSeconds(5));
    }

    private async void OnServerDiscovered(object? sender, ServerInformation server)
    {
        await _discovery.ConnectToServerAsync(server);
    }

    private async void OnConnected(object? sender, IConnectedServer server)
    {
        server.MessageReceived += (_, message) =>
        {
            Console.WriteLine($"Server says: {message}");
        };

        await server.SendMessageAsync("hello from the client");
    }
}
```

## Manual Client Targeting

If you do not want typed clients to update automatically when discovery finds a server, pass `false`:

```csharp
mauiServer.AddHttpClient<MyServerApiClient>(updateHttpClientsOnDiscovery: false);
mauiServer.AddQueueClient<MyQueueClient>(updateQueueClientsOnDiscovery: false);
```

Then point them manually:

```csharp
using Huddle.Client;
using Huddle.Client.Extensions;

httpClient.PointAtServer("192.168.1.25", 11000);
queueClient.PointAtServer("192.168.1.25", 54321);
```

## Sample

The client sample is in `samples/Huddle.Sample.Client`.

```bash
dotnet build samples/Huddle.Sample.Client.sln
```

## License And Attribution

Huddle.Client is open source under the Apache License 2.0.

Attribution to Pendragon Development is appreciated. If this package helps your project, a mention of Pendragon Development or a link back to the Huddle repository is welcome, but not required by the license.
