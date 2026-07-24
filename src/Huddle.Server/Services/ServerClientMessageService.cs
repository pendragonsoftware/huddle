using Huddle.Core;
using Huddle.Core.Services.Interfaces;

namespace Huddle.Server.Services;

public record SendToPeersResult(string[] IpAddresses, string[] UnableToSendTo);

public record ConnectedClient(string DeviceId, string IpAddress);

internal class ServerClientMessagingService
{
    private readonly IMessagingService _messagingService;

    private readonly Dictionary<string, (string IpAddress, string DeviceId, int Port, bool LastSentMessageFailed)> _peers = [];

    public event EventHandler<(string Message, string IpAddress)>? RecievedMessage;
    public event EventHandler<(string DeviceId, string IpAddress)>? ClientConnected;

    public ServerClientMessagingService(IMessagingService messagingService)
    {
        _messagingService = messagingService;
        _messagingService.MessageReceived += MessagingService_MessageReceived;
    }

    public ConnectedClient[] Clients => _peers
        .Select(x => new ConnectedClient(x.Value.DeviceId, x.Value.IpAddress))
        .ToArray();

    public virtual void Dispose()
    {
        _messagingService.MessageReceived -= MessagingService_MessageReceived;
        _messagingService?.Dispose();
    }

    public async Task<SendToPeersResult> SendMessageToPeersAsync(string message)
    {
        var sendTasks = new List<Task<(bool, string)>>();
        foreach (var peer in _peers)
        {
            var task = Task.Run(async () =>
            {
                var result = await _messagingService.SendAsync(message, peer.Value.IpAddress, peer.Value.Port);
                return (result, peer.Key);
            });
            sendTasks.Add(task);
        }
        var results = await Task.WhenAll(sendTasks);

        var sentTo = new List<string>();
        var notSentTo = new List<string>();
        foreach (var result in results)
        {
            // Since we dont track connections, mark in our list that last message failed
            var (ipAddress, deviceId, port, _) = _peers[result.Item2];
            _peers[result.Item2] = (ipAddress, deviceId, port, !result.Item1);

            if (!result.Item1)
            {
                notSentTo.Add(result.Item2);
            }
            else
            {
                sentTo.Add(result.Item2);
            }
        }

        return new SendToPeersResult([.. sentTo], [.. notSentTo]);
    }

    public async Task<int> StartListeningAsync() => await _messagingService.StartListeningAsync();

    public Task StopListeningAsync() => _messagingService.StopListeningAsync();

    private async void MessagingService_MessageReceived(object? sender, UdpMessage e)
    {
        if (e.Message.StartsWith(Constants.CONNECTION_MESSAGE_PREFIX))
        {
            if (!Huddle.Core.Services.ConnectionMessageParser.TryParseRequest(e.Message, out var deviceId, out var ipAddress, out var port))
            {
                return;
            }

            if (_peers.ContainsKey(deviceId))
            {
                _peers[deviceId] = (ipAddress, deviceId, port, false);
            }
            else
            {
                _peers.Add(deviceId, (ipAddress, deviceId, port, false));
            }

            await _messagingService.SendAsync(
                Huddle.Core.Services.ConnectionMessageFormatter.FormatConfirmation(deviceId),
                e.FromIpAddress,
                e.FromPort);

            ClientConnected?.Invoke(this, (deviceId, ipAddress));
        }
        else
        {
            RecievedMessage?.Invoke(this, (e.Message, e.FromIpAddress));
        }
    }
}
