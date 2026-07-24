using Huddle.Core.Services;

namespace Huddle.Core.Tests.Services;

public class QueueMessageTests
{
    [Fact]
    public void Queue_message_round_trips_when_payload_contains_colons()
    {
        var message = QueueMessageFormatter.Format("orders", "192.168.1.20", "created:123:priority");

        var parsed = QueueMessageParser.TryParse(
            message,
            out var queueName,
            out var sourceIpAddress,
            out var messageContent);

        Assert.True(parsed);
        Assert.Equal("orders", queueName);
        Assert.Equal("192.168.1.20", sourceIpAddress);
        Assert.Equal("created:123:priority", messageContent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("orders")]
    [InlineData("orders:")]
    [InlineData(":192.168.1.20:created")]
    [InlineData("orders::created")]
    public void Queue_message_parser_rejects_malformed_messages(string message)
    {
        var parsed = QueueMessageParser.TryParse(
            message,
            out var queueName,
            out var sourceIpAddress,
            out var messageContent);

        Assert.False(parsed);
        Assert.Equal(string.Empty, queueName);
        Assert.Equal(string.Empty, sourceIpAddress);
        Assert.Equal(string.Empty, messageContent);
    }
}
