using FluentAssertions;
using MLS.Broker.Configuration;
using Xunit;

namespace MLS.Broker.Tests;

/// <summary>
/// Tests for <see cref="BrokerOptions"/> defaults and configuration.
/// </summary>
public sealed class BrokerOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveSensibleValues()
    {
        var opts = new BrokerOptions();

        opts.HttpEndpoint.Should().Be("http://broker:5800");
        opts.WsEndpoint.Should().Be("ws://broker:6800");
        opts.BlockControllerUrl.Should().Be("http://block-controller:5100");
        opts.HyperliquidRestUrl.Should().Be("https://api.hyperliquid.xyz");
        opts.HyperliquidWsUrl.Should().Be("wss://api.hyperliquid.xyz/ws");
        opts.FallbackChain.Should().ContainSingle().Which.Should().Be("hyperliquid");
        opts.OrderTimeoutSeconds.Should().BeGreaterThan(0);
        opts.FillChannelCapacity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FallbackChain_DefaultFirstVenue_IsHyperliquid()
    {
        var opts = new BrokerOptions();
        opts.FallbackChain[0].Should().Be("hyperliquid");
    }

    [Fact]
    public void PostgresConnectionString_ContainsMlsDb()
    {
        var opts = new BrokerOptions();
        opts.PostgresConnectionString.Should().Contain("mls_db");
    }
}
