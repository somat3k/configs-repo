using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MLS.Broker.Configuration;
using MLS.Broker.Interfaces;
using MLS.Broker.Models;
using MLS.Broker.Services;
using Xunit;

namespace MLS.Broker.Tests;

/// <summary>
/// Tests for <see cref="BrokerFallbackChain"/>.
/// </summary>
public sealed class BrokerFallbackChainTests
{
    private static BrokerOptions DefaultOptions(string[]? chain = null) => new()
    {
        FallbackChain      = chain ?? ["hyperliquid"],
        OrderTimeoutSeconds = 5,
    };

    private static IOptions<BrokerOptions> OptionsOf(BrokerOptions opts)
        => Options.Create(opts);

    private static OrderResult SuccessResult(PlaceOrderRequest req) => new(
        req.ClientOrderId, "v-1", OrderState.Open, 0m, null,
        "hyperliquid", req.Symbol, req.Side,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static PlaceOrderRequest SampleRequest() =>
        new("BTC-USDT", OrderSide.Buy, OrderType.Limit, 0.5m, 65_000m, null,
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

        result.State.Should().Be(OrderState.Open);
        result.ClientOrderId.Should().Be(req.ClientOrderId);
    }

    [Fact]
    public async Task ExecuteWithFallback_ThrowsWhenAllVenuesFail()
    {
        var req     = SampleRequest();
        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.PlaceOrderAsync(req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(SuccessResult(req) with { State = OrderState.Rejected });

        var chain = new BrokerFallbackChain(primary.Object, OptionsOf(DefaultOptions()),
            NullLogger<BrokerFallbackChain>.Instance);

        var act = () => chain.ExecuteWithFallbackAsync(req, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("*All broker venues*");
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
    public async Task GetActiveBrokers_ReturnsPrimaryWhenReachable()
    {
        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.GetOpenOrdersAsync(string.Empty, It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<OrderResult>());

        var chain = new BrokerFallbackChain(primary.Object,
            OptionsOf(DefaultOptions(["hyperliquid"])),
            NullLogger<BrokerFallbackChain>.Instance);

        var active = await chain.GetActiveBrokersAsync(CancellationToken.None);
        active.Should().Contain("hyperliquid");
    }

    [Fact]
    public async Task GetActiveBrokers_ReturnsEmpty_WhenPrimaryUnreachable()
    {
        var primary = new Mock<IHyperliquidClient>();
        primary.Setup(c => c.GetOpenOrdersAsync(string.Empty, It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("timeout"));

        var chain = new BrokerFallbackChain(primary.Object,
            OptionsOf(DefaultOptions(["hyperliquid"])),
            NullLogger<BrokerFallbackChain>.Instance);

        var active = await chain.GetActiveBrokersAsync(CancellationToken.None);
        active.Should().BeEmpty();
    }
}
