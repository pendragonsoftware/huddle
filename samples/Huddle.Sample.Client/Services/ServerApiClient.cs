using Huddle.Client;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Huddle.Sample.Client.Services;

public class ServerApiClient(
    HttpClient httpClient,
    ILogger<ServerApiClient> logger)
{
    public void PointAtServer(string serverIpAddress, int port) => httpClient.PointAtServer(serverIpAddress, port);

    public string? Url => httpClient.BaseAddress != null ? $"{httpClient.BaseAddress.Host}:{httpClient.BaseAddress.Port}" : null;

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("status");
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync<string>("message", message);
        }
        catch (TaskCanceledException)
        {
            return false;
        }

        logger.LogInformation("Sent message {message}", message);

        if (response.IsSuccessStatusCode)
        {
            var responseText = await response.Content.ReadAsStringAsync();

            logger.LogInformation("Recieved response {response}", responseText);

            return true;
        }
        else
        {
            logger.LogInformation("Error sending message {statusCode}", response.StatusCode);
            return false;
        }
    }

    public async Task StartLoadTestAsync(int amount)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync($"loadtest/start?amountExpected={amount}", null);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        logger.LogInformation("Sent start load test {amount}", amount);
        var responseText = await response.Content.ReadAsStringAsync();

        logger.LogInformation("Recieved response {response}", responseText);
    }

    public async Task ContinueLoadTestAsync(int number)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync($"loadtest/continue?number={number}", null);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        logger.LogInformation("Sent continue load test");
        var responseText = await response.Content.ReadAsStringAsync();

        logger.LogInformation("Recieved response {response}", responseText);
    }

    public async Task<string> EndLoadTestAsync()
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync("loadtest/end", null);
        }
        catch (TaskCanceledException)
        {
            return string.Empty;
        }

        logger.LogInformation("Sent end load test");
        var responseText = await response.Content.ReadAsStringAsync();

        logger.LogInformation("Recieved response {response}", responseText);

        return responseText;
    }
}
