namespace Huddle.PeerToPeer;

public record SendToPeersResult(string[] IpAddresses, string[] UnableToSendTo);

public interface IMessagingService : IDisposable
{
    bool IsBroadcasting { get; }
    string DisplayName { get; }

    event EventHandler<bool>? IsBroadcastingChanged;
    event EventHandler<string>? PeerDiscovered;
    event EventHandler<string>? PeerLost;
    event EventHandler<(string Message, string DisplayName)>? MessageRecieved;

    Task StartAsync();

    Task StopAsync();

    Task<bool> SendAsync(string message, string displayName);

    Task<SendToPeersResult> SendToAllAsync(string message);
}
