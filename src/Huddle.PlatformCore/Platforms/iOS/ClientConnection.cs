using Foundation;
using Microsoft.Extensions.Logging;
using Network;

namespace Huddle.Core.Platforms.iOS;

internal class ClientConnection
{
    private readonly NWConnection _connection;
    private readonly Action<string, string, int> _recievedMessage;
    private readonly ILogger _logger;

    private TaskCompletionSource<bool> _ready;

    public ClientConnection(
        NWConnection connection,
        Action<string, string, int> recievedMessage,
        ILogger logger)
    {
        _connection = connection;
        _recievedMessage = recievedMessage;
        _logger = logger;

        IpAddress = _connection.Endpoint?.Address ?? string.Empty;
        Port = _connection.Endpoint?.PortNumber ?? 0;

        _ready = new();
        _connection.SetStateChangeHandler(Connection_StateChanged);
        _connection.SetQueue(CoreFoundation.DispatchQueue.MainQueue);
        _connection.Start();
    }

    public NWConnection Connection => _connection;

    public string IpAddress { get; private set; } = string.Empty;
    public int Port { get; private set; }

    public async Task WaitUntilReady() => await _ready.Task;

    private void Connection_StateChanged(NWConnectionState state, NWError? error)
    {
        if (error != null)
        {
            _logger.LogError("Client connection {ipAddress} - {errorCode}:{errorDescription}", _connection.Endpoint?.Address, error.ErrorCode, error);
            _ready.SetResult(false);
        }

        _logger.LogDebug("Client connection {ipAddress} changed {state}", _connection.Endpoint?.Address, state);

        if (state == NWConnectionState.Failed)
        {
            _logger.LogError("Client connection {ipAddress} - failed", _connection.Endpoint?.Address);
            _connection.Cancel();
            _ready.SetResult(false);
        }

        if (state == NWConnectionState.Ready)
        {
            _connection.ReceiveMessage(Connection_ReceiveMessage);
            _ready.SetResult(true);
        }
    }

    private void Connection_ReceiveMessage(nint data, nuint dataSize, NWContentContext? context, bool isComplete, NWError? error)
    {
        if (error != null)
        {
            _logger.LogError("Client connection: Recieved message {errorCode}:{errorDescription}", error.ErrorCode, error);
            return;
        }

        if (!isComplete)
        {
            return;
        }

        var dataBytes = NSData.FromBytes(data, dataSize);
        var message = NSString.FromData(dataBytes, NSStringEncoding.ASCIIStringEncoding)?.ToString();
        if (message == null)
        {
            _logger.LogWarning("Client connection {ipAddress} received an empty message payload", _connection.Endpoint?.Address);
            return;
        }

        _logger.LogDebug("Client connection {ipAddress} recieved {message}", _connection.Endpoint?.Address, message);

        _recievedMessage(message, _connection.Endpoint?.Address.ToString() ?? string.Empty, _connection.Endpoint?.PortNumber ?? 0);

        _connection.ReceiveMessage(Connection_ReceiveMessage);
    }
}
