using Huddle.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Network;
using System.Text;

namespace Huddle.Core.Platforms.iOS
{
    internal abstract class NWListenerConnectionService(ILogger<NWListenerConnectionService> logger) : IDisposable
    {
        protected readonly List<ClientConnection> _connections = [];

        private bool _isListening = false;
        private NWListener? _listener;
        private TaskCompletionSource<bool> _taskCompletionSource = new();

        public bool IsListening => _isListening;

        public event EventHandler<UdpMessage>? MessageReceived;

        public void Dispose()
        {
            _listener?.Dispose();
        }

        public async Task<int> StartListeningAsync(string? ipAddress = null, int? port = null)
        {
            var udpParams = NWParameters.CreateUdp();
            SetupUdpParams(udpParams);
            _listener = NWListener.Create(port?.ToString() ?? "0", udpParams)!;

            _listener.SetNewConnectionHandler(Listener_NewConnection);
            _listener.SetStateChangedHandler(Listener_StateChange);

            _listener.SetQueue(CoreFoundation.DispatchQueue.MainQueue);

            ListenerBeforeStart(_listener!);

            _listener.Start();

            await _taskCompletionSource.Task;

            return _listener.Port;
        }

        public Task StopListeningAsync()
        {
            try
            {
                foreach (var connection in _connections)
                {
                    connection.Connection.Cancel();
                }
            }
            catch (Exception) { }
            finally
            {
                _connections.Clear();
            }

            _listener?.Cancel();
            return Task.CompletedTask;
        }

        public async Task<bool> SendAsync(string message, string ipAddress, int port)
        {
            var connection = _connections.FirstOrDefault(x => x.IpAddress == ipAddress && x.Port == port);
            if (connection == null)
            {
                var endpoint = NWEndpoint.Create(ipAddress, port.ToString());
                var newConnection = new NWConnection(endpoint!, NWParameters.CreateUdp());
                connection = new ClientConnection(
                    newConnection,
                    (string ipAddress, string message, int port) => RecieveMessage(newConnection, ipAddress, message, port),
                    logger);
                _connections.Add(connection);
            }
            await connection.WaitUntilReady();

            return await SendAsync(message, connection.Connection);
        }

        protected async Task<bool> SendAsync(string message, NWConnection connection)
        {
            // TODO: custom code for send timeout?
            var encoded = Encoding.ASCII.GetBytes(message);
            var tcs = new TaskCompletionSource<bool>();
            connection.Send(encoded, NWContentContext.DefaultMessage, true, error =>
            {
                if (error != null)
                {
                    logger.LogError("NWConnectionMessagingService: Send message {errorCode}:{errorDescription}", error.ErrorCode, error);
                    tcs.SetResult(false);
                }
                else
                {
                    tcs.SetResult(true);
                }
            });

            return await tcs.Task;
        }
        
        protected virtual void SetupUdpParams(NWParameters parameters)
        {

        }

        protected virtual void ListenerBeforeStart(NWListener listener)
        {

        }

        protected virtual bool ShouldAcceptConnection(NWConnection connection)
        {
            return true;
        }

        private void Listener_StateChange(NWListenerState state, NWError? error)
        {
            if (error != null)
            {
                logger.LogError("NWConnectionMessagingService: Listener {errorCode}:{errorDescription}", error.ErrorCode, error);
                _taskCompletionSource.SetResult(false);
            }

            logger.LogDebug("NWConnectionMessagingService: Listener {state} changed", state);

            if (state == NWListenerState.Failed)
            {
                logger.LogError("NWConnectionMessagingService: Failed");
                _listener?.Cancel();
                _taskCompletionSource.SetResult(false);
            }

            if (state == NWListenerState.Ready)
            {
                logger.LogDebug("NWConnectionMessagingService: Listener ready");
                _isListening = true;
                _taskCompletionSource.SetResult(true);
            }
        }

        private void Listener_NewConnection(NWConnection connection)
        {
            if (!ShouldAcceptConnection(connection))
            {
                logger.LogDebug("NWConnectionMessagingService: New connection rejected {ipAddress}", connection.Endpoint?.Address);
                return;
            }

            logger.LogDebug("NWConnectionMessagingService: New connection accepted {ipAddress}", connection.Endpoint?.Address);
            _connections.Add(new ClientConnection(
                connection,
                (string ipAddress, string message, int port) => RecieveMessage(connection, ipAddress, message, port),
                logger));
        }

        private void RecieveMessage(NWConnection _, string message, string ipAddress, int port)
        {
            MessageReceived?.Invoke(this, new UdpMessage(message, ipAddress, port));
        }
    }
}
