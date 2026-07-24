using Huddle.Client;
using Microsoft.Extensions.Logging;

namespace Huddle.Sample.Client.Services;

public class ServerQueueClient(QueueClient queueClient, ILogger<ServerQueueClient> logger)
{
    public void PointAtServer(string serverIpAddress, int port) => queueClient.PointAtServer(serverIpAddress, port);

    public string? Url => queueClient.BaseAddress != null ? $"{queueClient.BaseAddress.Host}:{queueClient.BaseAddress.Port}" : null;

    public async Task<bool> SendMessageAsync(string queueName, string message)
    {
        logger.LogDebug("Sending message {message} to {url}", message, Url);

        var response = await queueClient.SendMessageAsync(queueName, message);

        logger.LogDebug("Sent message {message} to {url} recieved {response}", message, Url, response);

        return response;
    }
}
