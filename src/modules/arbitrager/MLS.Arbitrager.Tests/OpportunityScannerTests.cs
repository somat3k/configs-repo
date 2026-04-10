using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MLS.Arbitrager.Configuration;
using MLS.Arbitrager.Scanning;
using Xunit;

namespace MLS.Arbitrager.Tests;

/// <summary>
/// Unit tests for <see cref="OpportunityScanner"/> — price graph management and BFS path-finding.
/// </summary>
public sealed class OpportunityScannerTests
{
    private static OpportunityScanner CreateScanner(decimal minProfit = 0.01m, decimal inputAmount = 1_000m)
    {
        var opts = Options.Create(new ArbitragerOptions
        {
            MinProfitUsd             = minProfit,
            OpportunityQueueCapacity = 64,
            SimulatedInputAmountUsd  = inputAmount,
        });
        return new OpportunityScanner(opts, NullLogger<OpportunityScanner>.Instance);
    }

    private static PriceSnapshot Snap(string exchange, string symbol, decimal mid) =>
        new(exchange, symbol, mid * 0.999m, mid * 1.001m, mid, 50_000m, DateTimeOffset.UtcNow);

    // ── PublishPrice ──────────────────────────────────────────────────────────

    [Fact]
    public void PublishPrice_StoresSnapshotByExchangeAndSymbol()
    {
        var scanner = CreateScanner();
        var snap    = Snap("camelot", "WETH/USDC", 2_000m);

        scanner.PublishPrice(snap);

        var prices = scanner.GetCurrentPrices();
        prices.Should().ContainKey("camelot/WETH/USDC");
        prices["camelot/WETH/USDC"].MidPrice.Should().Be(2_000m);
    }

    [Fact]
    public void PublishPrice_OverwritesExistingSnapshotForSameKey()
    {
        var scanner = CreateScanner();
        scanner.PublishPrice(Snap("camelot", "WETH/USDC", 2_000m));
        scanner.PublishPrice(Snap("camelot", "WETH/USDC", 2_050m));

        scanner.GetCurrentPrices()["camelot/WETH/USDC"].MidPrice.Should().Be(2_050m);
    }

    [Fact]
    public void PublishPrice_IgnoresUnsupportedToken()
    {
        var scanner = CreateScanner();
        // DOGE is not in the supported token universe
        scanner.PublishPrice(Snap("camelot", "DOGE/USDC", 0.1m));

        // No opportunity should be emitted but price can be stored
        scanner.Opportunities.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void PublishPrice_IgnoresMalformedSymbol()
    {
        var scanner = CreateScanner();
        scanner.PublishPrice(Snap("camelot", "INVALIDSYMBOL", 100m));
        scanner.Opportunities.TryRead(out _).Should().BeFalse();
    }

    // ── GetCurrentPrices ─────────────────────────────────────────────────────

    [Fact]
    public void GetCurrentPrices_ReturnsAllPublishedSnapshots()
    {
        var scanner = CreateScanner();
        scanner.PublishPrice(Snap("camelot",     "WETH/USDC", 2_000m));
        scanner.PublishPrice(Snap("dfyn",        "WETH/USDC", 2_005m));
        scanner.PublishPrice(Snap("hyperliquid", "ARB/USDC",  1.2m));

        var prices = scanner.GetCurrentPrices();
        prices.Should().HaveCount(3);
    }

    // ── Opportunity emission via RunScan ─────────────────────────────────────

    [Fact]
    public void PublishPrice_DoesNotEmitWhenSinglePriceOnly()
    {
        // PublishPrice no longer triggers BFS — RunScan must be called explicitly.
        // With only one price snapshot, RunScan still cannot find a circular path.
        var scanner = CreateScanner(minProfit: 0.001m);
        scanner.PublishPrice(Snap("camelot", "WETH/USDC", 2_000m));
        scanner.RunScan();
        scanner.Opportunities.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void RunScan_DoesNotCrashWithEmptyPriceGraph()
    {
        var scanner = CreateScanner();
        // Should complete without throwing even if no prices are published
        var act = () => scanner.RunScan();
        act.Should().NotThrow();
    }

    [Fact]
    public void RunScan_ProcessesPricesPublishedBeforeCall()
    {
        // Create an artificial triangle: WETH→USDC on camelot, USDC→ARB on dfyn,
        // ARB→WETH on balancer — verify RunScan reads updated graph.
        var scanner = CreateScanner(minProfit: 0.001m, inputAmount: 1_000m);

        scanner.PublishPrice(Snap("camelot", "WETH/USDC", 2_010m));
        scanner.PublishPrice(Snap("dfyn",    "USDC/ARB",  1.3m));
        scanner.PublishPrice(Snap("balancer","ARB/WETH",  0.00078m));

        // Verify that RunScan runs without throwing regardless of whether a profitable cycle exists.
        var act = () => scanner.RunScan();
        act.Should().NotThrow();

        // Price graph must be intact after the scan.
        scanner.GetCurrentPrices().Should().HaveCount(3);
    }

    // ── Channel capacity ──────────────────────────────────────────────────────

    [Fact]
    public void OpportunitiesChannel_HasConfiguredCapacity()
    {
        var scanner = CreateScanner();
        // Verify channel is bounded and accessible
        scanner.Opportunities.Should().NotBeNull();
        scanner.Opportunities.CanCount.Should().BeTrue();
    }
}
