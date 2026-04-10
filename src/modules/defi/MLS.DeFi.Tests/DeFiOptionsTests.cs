using FluentAssertions;
using MLS.DeFi.Configuration;
using Xunit;

namespace MLS.DeFi.Tests;

/// <summary>
/// Tests for <see cref="DeFiOptions"/> defaults and configuration.
/// </summary>
public sealed class DeFiOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveSensibleValues()
    {
        var opts = new DeFiOptions();

        opts.HttpEndpoint.Should().Be("http://defi:5500");
        opts.WsEndpoint.Should().Be("ws://defi:6500");
        opts.BlockControllerUrl.Should().Be("http://block-controller:5100");
        opts.HyperliquidRestUrl.Should().Be("https://api.hyperliquid.xyz");
        opts.HyperliquidWsUrl.Should().Be("wss://api.hyperliquid.xyz/ws");
        opts.FallbackChain.Should().ContainSingle().Which.Should().Be("hyperliquid");
        opts.OrderTimeoutSeconds.Should().BeGreaterThan(0);
        opts.PositionChannelCapacity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FallbackChain_DefaultFirstVenue_IsHyperliquid()
    {
        var opts = new DeFiOptions();
        opts.FallbackChain[0].Should().Be("hyperliquid");
    }

    [Fact]
    public void PostgresConnectionString_ContainsMlsDb()
    {
        var opts = new DeFiOptions();
        opts.PostgresConnectionString.Should().Contain("mls_db");
    }

    [Fact]
    public void ChainId_DefaultIsArbitrumOne()
    {
        var opts = new DeFiOptions();
        opts.ChainId.Should().Be(42161);
    }

    [Fact]
    public void ChainRpcUrl_DefaultIsArbitrumRpc()
    {
        var opts = new DeFiOptions();
        opts.ChainRpcUrl.Should().Contain("arbitrum");
    }

    [Fact]
    public void GasPriceMultiplier_DefaultIsAboveOne()
    {
        var opts = new DeFiOptions();
        opts.GasPriceMultiplier.Should().BeGreaterThan(1m);
    }

    [Fact]
    public void WalletBackend_DefaultIsEnv()
    {
        var opts = new DeFiOptions();
        opts.WalletBackend.Should().Be("env");
    }
}
