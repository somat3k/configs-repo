using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MLS.DeFi.Configuration;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;
using MLS.DeFi.Services;
using Xunit;

namespace MLS.DeFi.Tests;

/// <summary>
/// Tests for <see cref="BrokerFallbackChain"/>.
/// </summary>
public sealed class BrokerFallbackChainTests
{
    private static DeFiOptions DefaultOptions(string[]? chain = null) => new()
    {
        FallbackChain       = chain ?? ["hyperliquid"],
        OrderTimeoutSeconds = 5,
    };

    private static IOptions<DeFiOptions> OptionsOf(DeFiOptions opts)
        => Options.Create(opts);

    private static DeFiOrderResult SuccessResult(DeFiOrderRequest req) => new(
        req.ClientOrderId, "v-1", DeFiOrderState.Open, 0m, null,
        "hyperliquid", req.Symbol, req.Side,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static DeFiOrderRequest SampleRequest() =>
        new("BTC-USDT", DeFiOrderSide.Buy, DeFiOrderType.Limit, 0.5m, 65_000m, null,
            Guid.NewGuid().ToString(), "trader");

    // ── ExecuteWithFallbackAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteWithFallback_ReturnsPrimaryResultWhenSuccessful()
    {
        var req     = SampleRequest();
        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.PlaceOrderAsync(req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(SuccessResult(req));

        var chain = new BrokerFallbackChain(primary.Object, OptionsOf(DefaultOptions()),
            NullLogger<BrokerFallbackChain>.Instance);

        var result = await chain.ExecuteWithFallbackAsync(req, CancellationToken.None);

        result.State.Should().Be(DeFiOrderState.Open);
        result.ClientOrderId.Should().Be(req.ClientOrderId);
    }

    [Fact]
    public async Task ExecuteWithFallback_ThrowsWhenAllVenuesFail()
    {
        var req     = SampleRequest();
        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.PlaceOrderAsync(req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(SuccessResult(req) with { State = DeFiOrderState.Rejected });

        var chain = new BrokerFallbackChain(primary.Object, OptionsOf(DefaultOptions()),
            NullLogger<BrokerFallbackChain>.Instance);

        var act = () => chain.ExecuteWithFallbackAsync(req, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("*All DeFi broker venues*");
    }

    [Fact]
    public async Task ExecuteWithFallback_ThrowsWhenPrimaryThrows()
    {
        var req     = SampleRequest();
        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.PlaceOrderAsync(req, It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Connection refused"));

        var chain = new BrokerFallbackChain(primary.Object, OptionsOf(DefaultOptions()),
            NullLogger<BrokerFallbackChain>.Instance);

        var act = () => chain.ExecuteWithFallbackAsync(req, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── GetActiveBrokersAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveBrokers_ReturnsHyperliquidWhenReachable()
    {
        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.GetOpenOrdersAsync(string.Empty, It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<DeFiOrderResult>());

        var chain = new BrokerFallbackChain(primary.Object,
            OptionsOf(DefaultOptions(["hyperliquid"])),
            NullLogger<BrokerFallbackChain>.Instance);

        var active = await chain.GetActiveBrokersAsync(CancellationToken.None);

        active.Should().ContainSingle().Which.Should().Be("hyperliquid");
    }

    [Fact]
    public async Task GetActiveBrokers_ReturnsEmptyWhenPrimaryThrows()
    {
        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.GetOpenOrdersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Unreachable"));

        var chain = new BrokerFallbackChain(primary.Object, OptionsOf(DefaultOptions()),
            NullLogger<BrokerFallbackChain>.Instance);

        var active = await chain.GetActiveBrokersAsync(CancellationToken.None);

        active.Should().BeEmpty();
    }

    // ── Side / Type enum coverage ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteWithFallback_SellOrder_RoutedCorrectly()
    {
        var req = new DeFiOrderRequest("ETH-USDT", DeFiOrderSide.Sell, DeFiOrderType.Market,
            1m, null, null, Guid.NewGuid().ToString(), "defi");

        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.PlaceOrderAsync(req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new DeFiOrderResult(req.ClientOrderId, "v-2", DeFiOrderState.Open,
                   0m, null, "hyperliquid", req.Symbol, req.Side,
                   DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var chain = new BrokerFallbackChain(primary.Object, OptionsOf(DefaultOptions()),
            NullLogger<BrokerFallbackChain>.Instance);

        var result = await chain.ExecuteWithFallbackAsync(req, CancellationToken.None);

        result.Side.Should().Be(DeFiOrderSide.Sell);
        result.State.Should().Be(DeFiOrderState.Open);
    }
}
