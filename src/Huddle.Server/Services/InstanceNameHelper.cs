namespace Huddle.Server.Services;

internal static class InstanceNameHelper
{
    public static string Format(string ipAddress, int? httpPort, int? queuePort, int? listeningPort)
    {
        var sIpAddress = Core.Services.InstanceNameFormatter.GetIpAddress(ipAddress);
        var sHttpPort = Core.Services.InstanceNameFormatter.GetPort(httpPort);
        var sQueuePort = Core.Services.InstanceNameFormatter.GetPort(queuePort);
        var sListeningPort = Core.Services.InstanceNameFormatter.GetPort(listeningPort);

        return $"{sIpAddress}:{sHttpPort}:{sQueuePort}:{sListeningPort}";
    }
}
