using FluentAssertions;
using MLS.Core.Constants;
using Xunit;

namespace MLS.Broker.Tests;

/// <summary>
/// Tests for broker-related <see cref="MessageTypes"/> constants.
/// </summary>
public sealed class MessageTypesTests
{
    [Theory]
    [InlineData(MessageTypes.OrderCreate,       "ORDER_CREATE")]
    [InlineData(MessageTypes.OrderCancel,       "ORDER_CANCEL")]
    [InlineData(MessageTypes.OrderConfirmation, "ORDER_CONFIRMATION")]
    [InlineData(MessageTypes.FillNotification,  "FILL_NOTIFICATION")]
    [InlineData(MessageTypes.PositionUpdate,    "POSITION_UPDATE")]
    public void BrokerMessageType_HasExpectedValue(string actual, string expected)
        => actual.Should().Be(expected);

    [Fact]
    public void BrokerMessageTypes_AreNonEmpty()
    {
        MessageTypes.OrderCreate.Should().NotBeNullOrWhiteSpace();
        MessageTypes.OrderCancel.Should().NotBeNullOrWhiteSpace();
        MessageTypes.OrderConfirmation.Should().NotBeNullOrWhiteSpace();
        MessageTypes.FillNotification.Should().NotBeNullOrWhiteSpace();
        MessageTypes.PositionUpdate.Should().NotBeNullOrWhiteSpace();
    }
}
