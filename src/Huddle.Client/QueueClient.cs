using Huddle.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Huddle.Client
{
    public class QueueClient
    {
        private readonly IMessagingService _service;
        private readonly IIpAddressRetrievalService _ipAddressRetrievalService;
        private readonly ILogger _logger;

        private IPEndPoint? _endpoint;

        public Uri? BaseAddress => _endpoint != null ? new Uri($"http://{_endpoint.Address}:{_endpoint.Port}/") : null;

        public QueueClient(IMessagingService messagingService, IIpAddressRetrievalService ipAddressRetrievalService, ILogger<QueueClient> logger)
        {
            _service = messagingService;
            _ipAddressRetrievalService = ipAddressRetrievalService;
            _logger = logger;
        }

        public void PointAtServer(string hostAddress, int port)
        {
            _endpoint = new IPEndPoint(IPAddress.Parse(hostAddress), port);
        }

        public async Task<bool> SendMessageAsync(string queueName, string message)
        {
            if (_endpoint == null)
            {
                return false;
            }

            var myIpAddress = _ipAddressRetrievalService.GetIpAddress();

            _logger.LogDebug("Connecting to {ipAddress}:{endpoint}", _endpoint.Address.ToString(), _endpoint.Port);

            var formattedMessage = Huddle.Core.Services.QueueMessageFormatter.Format(queueName, myIpAddress, message);
            return await _service.SendAsync(formattedMessage, _endpoint.Address.ToString(), _endpoint.Port);
        }
    }
}
