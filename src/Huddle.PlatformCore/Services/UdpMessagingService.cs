using Huddle.Core.Services.Interfaces;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Huddle.Core.Services
{
    internal partial class UdpMessagingService : IMessagingService
    {
        private const int SEND_TIMEOUT_SECONDS = 5;

        private bool _isListening = false;
        private UdpClient? _listener;
        private CancellationTokenSource? _cancellationTokenSource;

        public bool IsListening => _isListening;

        public event EventHandler<UdpMessage>? MessageReceived;

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();

            if (_isListening)
            {
                _isListening = false;
            }

            _listener?.Dispose();
        }

        public Task<int> StartListeningAsync(string? ipAddress = null, int? port = null)
        {
            if (_isListening)
            {
                return Task.FromResult(((IPEndPoint)_listener!.Client.LocalEndPoint!).Port);
            }

            _listener = new UdpClient(port ?? 0, AddressFamily.InterNetwork);
            _listener.Client.SendTimeout = SEND_TIMEOUT_SECONDS * 1000;
            _cancellationTokenSource = new();

            var assignedPort = ((IPEndPoint)_listener.Client.LocalEndPoint!).Port;

            _isListening = true;
            _ = Task.Run(async () => await StartListeningLoop(_listener, e => MessageReceived?.Invoke(this, e)));

            return Task.FromResult(assignedPort);
        }

        public async Task StopListeningAsync()
        {
            _isListening = false;

            if (_cancellationTokenSource != null)
            {
                await _cancellationTokenSource.CancelAsync();
                _cancellationTokenSource = null;
            }

            if (_listener != null)
            {
                try
                {
                    await _listener.Client.DisconnectAsync(true);
                }
                catch (Exception) { }

                _listener.Dispose();
                _listener = null;
            }
        }

        public async Task<bool> SendAsync(string message, string ipAddress, int port)
        {
            if (_listener == null)
            {
                _listener = new UdpClient();
                _listener.Client.SendTimeout = SEND_TIMEOUT_SECONDS * 1000;
            }

            var response = Encoding.ASCII.GetBytes(message);
            var ipEndpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

            var sendResult = await _listener.SendAsync(
                response,
                ipEndpoint,
                _cancellationTokenSource?.Token ?? CancellationToken.None);

            return sendResult > 0;
        }

        private async Task StartListeningLoop(UdpClient listener, Action<UdpMessage> messageRecieved)
        {
            while (_isListening)
            {
                try
                {
                    var result = await listener.ReceiveAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);
                    var message = Encoding.ASCII.GetString(result.Buffer);

                    messageRecieved(new UdpMessage(
                        message,
                        result.RemoteEndPoint.Address.ToString(),
                        result.RemoteEndPoint.Port));
                }
                catch (Exception)
                {
                    // If cancelled just swallow the error - likely because the listener has been stopped
                    if (!_isListening)
                    {
                        throw;
                    }
                }
            }
        }
    }
}
