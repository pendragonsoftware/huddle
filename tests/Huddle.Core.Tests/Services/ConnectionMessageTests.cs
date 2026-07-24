using Huddle.Core.Services;

namespace Huddle.Core.Tests.Services;

public class ConnectionMessageTests
{
    [Fact]
    public void Request_round_trips()
    {
        var message = ConnectionMessageFormatter.FormatRequest("device-1", "192.168.1.10", 4567);

        var parsed = ConnectionMessageParser.TryParseRequest(
            message,
            out var deviceId,
            out var ipAddress,
            out var listeningPort);

        Assert.True(parsed);
        Assert.Equal("device-1", deviceId);
        Assert.Equal("192.168.1.10", ipAddress);
        Assert.Equal(4567, listeningPort);
    }

    [Fact]
    public void Confirmation_round_trips()
    {
        var message = ConnectionMessageFormatter.FormatConfirmation("device-1");

        var parsed = ConnectionMessageParser.TryParseConfirmation(message, out var deviceId);

        Assert.True(parsed);
        Assert.Equal("device-1", deviceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("__CONNECT__")]
    [InlineData("__CONNECT__device-only")]
    [InlineData("__CONNECT__device:192.168.1.10:not-a-port")]
    [InlineData("__CONNECT__device:192.168.1.10:0")]
    public void Request_parser_rejects_malformed_messages(string message)
    {
        var parsed = ConnectionMessageParser.TryParseRequest(
            message,
            out var deviceId,
            out var ipAddress,
            out var listeningPort);

        Assert.False(parsed);
        Assert.Equal(string.Empty, deviceId);
        Assert.Equal(string.Empty, ipAddress);
        Assert.Equal(0, listeningPort);
    }
}
