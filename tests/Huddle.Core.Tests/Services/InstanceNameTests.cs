using Huddle.Core.Services;

namespace Huddle.Core.Tests.Services;

public class InstanceNameTests
{
    [Theory]
    [InlineData("192.168.1.10")]
    [InlineData("10.0.0.5")]
    public void Ip_address_round_trips(string ipAddress)
    {
        var formatted = InstanceNameFormatter.GetIpAddress(ipAddress);

        var parsed = InstanceNameParser.TryGetIpAddress(formatted, out var parsedIpAddress);

        Assert.True(parsed);
        Assert.Equal(ipAddress, parsedIpAddress);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5000)]
    [InlineData(65535)]
    public void Port_round_trips(int port)
    {
        var formatted = InstanceNameFormatter.GetPort(port);

        var parsed = InstanceNameParser.TryGetPort(formatted, out var parsedPort);

        Assert.True(parsed);
        Assert.Equal(port, parsedPort);
    }

    [Fact]
    public void Null_port_round_trips_as_empty_value()
    {
        var formatted = InstanceNameFormatter.GetPort(null);

        var parsed = InstanceNameParser.TryGetPort(formatted, out var parsedPort);

        Assert.True(parsed);
        Assert.Null(parsedPort);
    }

    [Fact]
    public void Attributes_parse_when_required_values_are_present()
    {
        var attributes = new Dictionary<string, string>
        {
            ["IpAddress"] = "192.168.1.10",
            ["DeviceId"] = "device-1",
            ["HttpPort"] = "5001",
            ["QueuePort"] = "5002",
            ["ListeningPort"] = "5003"
        };

        var parsed = InstanceNameParser.TryParseAttributes(
            attributes,
            out var ipAddress,
            out var deviceId,
            out var httpPort,
            out var queuePort,
            out var listeningPort);

        Assert.True(parsed);
        Assert.Equal("192.168.1.10", ipAddress);
        Assert.Equal("device-1", deviceId);
        Assert.Equal(5001, httpPort);
        Assert.Equal(5002, queuePort);
        Assert.Equal(5003, listeningPort);
    }
}
