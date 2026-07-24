using Huddle.Core;
using Huddle.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Huddle.Client.Services
{
    internal partial class ConnectedServer : IConnectedServer
    {
        private readonly IMessagingService _messagingService;
        private readonly ILogger _logger;

        public ConnectedServer(string deviceId, string ipAddress, int port, IMessagingService messagingService, ILogger logger)
        {
            DeviceId = deviceId;
            IpAddress = ipAddress;
            Port = port;

            _messagingService = messagingService;
            _logger = logger;

            _messagingService.MessageReceived += MessagingService_MessageReceived;
        }

        public string DeviceId { get; private set; }
        public string IpAddress { get; private set; }
        public int Port { get; private set; }

        public event EventHandler<string>? MessageReceived;

        public void Dispose()
        {
            _messagingService.MessageReceived -= MessagingService_MessageReceived;
        }

        public async Task<bool> SendMessageAsync(string message)
        {
            _logger.LogInformation("ConnectedServer: Sent {message} to server", message);
            return await _messagingService.SendAsync(message, IpAddress, Port);
        }

        private void MessagingService_MessageReceived(object? sender, UdpMessage e)
        {
            if (!e.Message.StartsWith(Constants.CONNECTION_MESSAGE_PREFIX))
            {
                MessageReceived?.Invoke(this, e.Message);
            }
        }
    }
}