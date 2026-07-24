# Huddle.PeerToPeer

`Huddle.PeerToPeer` is for .NET MAUI apps that both advertise themselves and discover nearby peers. It uses display names for peer discovery and lets peers send direct messages to each other on the local network.

## Install

```bash
dotnet add package Huddle.PeerToPeer
```

## Register Peer-To-Peer Messaging

Register a peer-to-peer service in `MauiProgram.cs`:

```csharp
using Huddle.PeerToPeer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        builder.Services.AddHuddlePeerToPeer("huddlepeers")
            .WithDisplayName($"device-{Guid.NewGuid():N}"[..13])
            .MapHandler<PeerMessageHandler>()
            .Build();

        return builder
            .Build()
            .StartHuddlePeerToPeerInBackground();
    }
}
```

The service name identifies the peer group. Apps must use the same service name to discover each other.

## Incoming Messages

Map an `IMessageHandler` to handle incoming peer messages.

```csharp
using Huddle.PeerToPeer;

public sealed class PeerMessageHandler : IMessageHandler
{
    public Task HandleMessageAsync(string message, string displayName)
    {
        Console.WriteLine($"{displayName}: {message}");
        return Task.CompletedTask;
    }
}
```

## Track Peers And Send Messages

Inject `Huddle.PeerToPeer.IMessagingService` to track peers and send messages:

```csharp
using Huddle.PeerToPeer;

public sealed class PeerChat
{
    private readonly IMessagingService _messaging;

    public PeerChat(IMessagingService messaging)
    {
        _messaging = messaging;

        _messaging.PeerDiscovered += (_, displayName) =>
        {
            Console.WriteLine($"Peer found: {displayName}");
        };

        _messaging.PeerLost += (_, displayName) =>
        {
            Console.WriteLine($"Peer lost: {displayName}");
        };

        _messaging.MessageRecieved += (_, message) =>
        {
            Console.WriteLine($"{message.DisplayName}: {message.Message}");
        };
    }

    public Task<bool> SendAsync(string displayName, string message)
    {
        return _messaging.SendAsync(message, displayName);
    }

    public Task<SendToPeersResult> BroadcastAsync(string message)
    {
        return _messaging.SendToAllAsync(message);
    }
}
```

## Sample

The peer-to-peer sample is in `samples/Huddle.Sample.PeerToPeer`.

```bash
dotnet build samples/Huddle.Sample.PeerToPeer.sln
```

## License And Attribution

Huddle.PeerToPeer is open source under the Apache License 2.0.

Attribution to Pendragon Development is appreciated. If this package helps your project, a mention of Pendragon Development or a link back to the Huddle repository is welcome, but not required by the license.
