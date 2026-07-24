namespace Huddle.Client
{
    public static class HttpClientExtensions
    {
        public static void PointAtServer(this HttpClient httpClient, string serverIpAddress, int port)
        {
            httpClient.BaseAddress = new Uri($"http://{serverIpAddress}:{port}/");
        }
    }
}
