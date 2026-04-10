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
/// Tests for <see cref="DeFiStrategyEngine"/>.
/// </summary>
public sealed class DeFiStrategyEngineTests
{
    private static DeFiOptions DefaultOptions() => new();

    private static DeFiStrategyRequest SampleRequest(
        string symbol = "BTC-USDT",
        DeFiOrderSide side = DeFiOrderSide.Buy,
        decimal qty = 1m,
        int slippageBps = 50)
        => new(symbol, side, qty, slippageBps, "defi");

    private static DeFiStrategyEngine BuildEngine(
        Mock<IHyperliquidClient>? hyperliquid = null,
        Mock<IBrokerFallbackChain>? fallback   = null)
    {
        var hl  = hyperliquid ?? new Mock<IHyperliquidClient>();
        var fb  = fallback    ?? new Mock<IBrokerFallbackChain>();

        hl.Setup(c => c.GetOpenOrdersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Array.Empty<DeFiOrderResult>());

        fb.Setup(c => c.GetActiveBrokersAsync(It.IsAny<CancellationToken>()))
          .ReturnsAsync(new[] { "hyperliquid" });

        return new DeFiStrategyEngine(hl.Object, fb.Object,
            NullLogger<DeFiStrategyEngine>.Instance);
    }

    // ── EvaluateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Evaluate_DefaultRequest_SelectsHyperliquid()
    {
        var engine = BuildEngine();
        var req    = SampleRequest();

        var result = await engine.EvaluateAsync(req, CancellationToken.None);

        result.StrategyType.Should().Be(DeFiStrategyType.HyperliquidPerpetual);
        result.Venue.Should().Be("hyperliquid");
    }

    [Fact]
    public async Task Evaluate_LowSlippage_LargeQty_SelectsCamelot()
    {
        var engine = BuildEngine();
        var req    = SampleRequest(qty: 15m, slippageBps: 5);

        var result = await engine.EvaluateAsync(req, CancellationToken.None);

        result.StrategyType.Should().Be(DeFiStrategyType.CamelotSpotSwap);
        result.Venue.Should().Be("camelot");
    }

    [Fact]
    public async Task Evaluate_EstimatedOutput_IsLessThanInputQuantity()
    {
        var engine = BuildEngine();
        var req    = SampleRequest(qty: 1m, slippageBps: 50);

        var result = await engine.EvaluateAsync(req, CancellationToken.None);

        result.EstimatedOutputQuantity.Should().BeLessThan(req.Quantity);
        result.EstimatedOutputQuantity.Should().BePositive();
    }

    [Fact]
    public async Task Evaluate_IncludesNonNullExecutionOrder()
    {
        var engine = BuildEngine();
        var req    = SampleRequest();

        var result = await engine.EvaluateAsync(req, CancellationToken.None);

        result.ExecutionOrder.Should().NotBeNull();
        result.ExecutionOrder!.Symbol.Should().Be(req.Symbol);
        result.ExecutionOrder.RequestingModuleId.Should().Be(req.RequestingModuleId);
    }

    // ── ExecuteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_SuccessfulStrategy_ReturnsResult()
    {
        var fallback = new Mock<IBrokerFallbackChain>();
        fallback.Setup(c => c.GetActiveBrokersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "hyperliquid" });

        fallback.Setup(c => c.ExecuteWithFallbackAsync(
                    It.IsAny<DeFiOrderRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeFiOrderResult(Guid.NewGuid().ToString(), "v-1",
                    DeFiOrderState.Open, 0m, null, "hyperliquid", "BTC-USDT",
                    DeFiOrderSide.Buy, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var engine = BuildEngine(fallback: fallback);
        var req    = SampleRequest();

        var result = await engine.ExecuteAsync(req, CancellationToken.None);

        result.StrategyType.Should().Be(DeFiStrategyType.HyperliquidPerpetual);
    }

    [Fact]
    public async Task Execute_AllVenuesFail_ThrowsInvalidOperationException()
    {
        var fallback = new Mock<IBrokerFallbackChain>();
        fallback.Setup(c => c.GetActiveBrokersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<string>());

        fallback.Setup(c => c.ExecuteWithFallbackAsync(
                    It.IsAny<DeFiOrderRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("All venues failed"));

        var engine = BuildEngine(fallback: fallback);
        var req    = SampleRequest();

        var act = () => engine.ExecuteAsync(req, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("*All venues failed*");
    }

    // ── GetAvailableVenuesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableVenues_IncludesHyperliquidAndProtocols()
    {
        var engine = BuildEngine();
        var venues = await engine.GetAvailableVenuesAsync(CancellationToken.None);

        venues.Should().Contain("hyperliquid");
        venues.Should().Contain("camelot");
        venues.Should().Contain("dfyn");
        venues.Should().Contain("balancer");
        venues.Should().Contain("morpho");
        venues.Should().Contain("nhop");
    }

    [Fact]
    public async Task GetAvailableVenues_DoesNotIncludeUniswap()
    {
        var engine = BuildEngine();
        var venues = await engine.GetAvailableVenuesAsync(CancellationToken.None);

        venues.Should().NotContain(v => v.Contains("uniswap", StringComparison.OrdinalIgnoreCase));
    }
}
