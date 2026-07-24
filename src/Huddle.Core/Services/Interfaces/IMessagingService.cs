namespace Huddle.Core.Services.Interfaces
{
    public record UdpMessage(string Message, string FromIpAddress, int FromPort);

    public interface IMessagingService : IDisposable
    {
        event EventHandler<UdpMessage>? MessageReceived;
        bool IsListening { get; }
        Task<bool> SendAsync(string message, string ipAddress, int port);
        Task<int> StartListeningAsync(string? ipAddress = null, int? port = null);
        Task StopListeningAsync();
    }
}