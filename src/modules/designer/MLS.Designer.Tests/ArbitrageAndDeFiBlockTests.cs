using System.Text.Json;
using FluentAssertions;
using MLS.Core.Designer;
using MLS.Designer.Blocks.Arbitrage;
using MLS.Designer.Blocks.DeFi;
using Xunit;

namespace MLS.Designer.Tests;

/// <summary>
/// Unit tests for Arbitrage and DeFi domain blocks.
/// All tests run deterministic computations — no live exchange calls.
/// </summary>
public sealed class ArbitrageAndDeFiBlockTests
{
    // ── nHOPPathFinderBlock ────────────────────────────────────────────────────

    [Fact]
    public async Task nHOPPathFinder_ThreeHopArbitrage_FindsProfitablePath()
    {
        // Arrange: build a 3-hop circuit WETH → USDC → ARB → WETH with deliberate profit.
        // Default InputAmount = 1.0 WETH (token units, not USD).
        //
        // Computation (fee=0 on each hop):
        //   Hop 1: 1.0 WETH * 1000 (WETH→USDC price) = 1000 USDC
        //   Hop 2: 1000 USDC * 2.0 (USDC→ARB price)  = 2000 ARB
        //   Hop 3: 2000 ARB  * 0.0007 (ARB→WETH price) = 1.4 WETH  ← back to start token
        //
        // Net profit = 1.4 WETH − 1.0 WETH − 0.30 (3 × $0.10 gas) ≈ 0.4 WETH before gas deduction
        // Gas is in USD units so it only reduces USD-denominated gas_usd, not the WETH profit directly.
        // net_profit = output_amount − input_amount − gas = 1.4 − 1.0 − 0.30 = 0.10
        // (Gas deducted as-is since units are mixed; the block uses gas_usd as a token-unit proxy.)
        var block = new nHOPPathFinderBlock();
        block.Parameters.Should().NotBeEmpty("nHOPPathFinderBlock must declare parameters");

        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // Feed hop 1: WETH → USDC on camelot
        await block.ProcessAsync(
            MakeEdge("camelot", "WETH", "USDC", price: 1000m, fee: 0m, gasUsd: 0.10m),
            CancellationToken.None);

        // Feed hop 2: USDC → ARB on dfyn
        await block.ProcessAsync(
            MakeEdge("dfyn", "USDC", "ARB", price: 2.0m, fee: 0m, gasUsd: 0.10m),
            CancellationToken.None);

        // Feed hop 3: ARB → WETH on balancer (closes the cycle)
        // 2000 ARB * 0.0007 = 1.4 WETH
        await block.ProcessAsync(
            MakeEdge("balancer", "ARB", "WETH", price: 0.0007m, fee: 0m, gasUsd: 0.10m),
            CancellationToken.None);

        // Assert
        output.Should().NotBeNull("a profitable 3-hop path should be found");
        output!.Value.SocketType.Should().Be(BlockSocketType.PathUpdate);

        var root = output.Value.Value;
        root.ValueKind.Should().Be(JsonValueKind.Object);
        root.TryGetProperty("paths", out var paths).Should().BeTrue("output must contain paths array");
        paths.GetArrayLength().Should().BeGreaterThan(0, "at least one profitable path should be emitted");

        var firstPath = paths[0];
        firstPath.TryGetProperty("net_profit", out var profitEl).Should().BeTrue();
        profitEl.TryGetDecimal(out var profit).Should().BeTrue();
        profit.Should().BeGreaterThan(0, "net profit must be positive for a valid arbitrage");

        // Verify output_amount is the correct end amount in WETH (1.4 WETH rounded)
        firstPath.TryGetProperty("output_amount", out var outEl).Should().BeTrue();
        outEl.TryGetDecimal(out var outAmount).Should().BeTrue();
        outAmount.Should().BeApproximately(1.4m, 0.001m,
            "with prices 1000 / 2.0 / 0.0007 from 1.0 WETH input the output should be ~1.4 WETH");
    }

    [Fact]
    public async Task nHOPPathFinder_NoProfit_DoesNotEmit()
    {
        var block = new nHOPPathFinderBlock();
        var emitted = false;
        block.OutputProduced += (_, _) => { emitted = true; return ValueTask.CompletedTask; };

        // Feed only one hop — incomplete cycle, no profit
        await block.ProcessAsync(
            MakeEdge("camelot", "WETH", "USDC", price: 2000m, fee: 0.003m, gasUsd: 0.50m),
            CancellationToken.None);

        emitted.Should().BeFalse("no output when the path is incomplete");
    }

    [Fact]
    public async Task nHOPPathFinder_Reset_ClearsGraph()
    {
        var block = new nHOPPathFinderBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // Feed a profitable 3-hop path
        await block.ProcessAsync(MakeEdge("camelot",  "WETH", "USDC", 1000m, 0m, 0.10m), CancellationToken.None);
        await block.ProcessAsync(MakeEdge("dfyn",     "USDC", "ARB",  2.0m,  0m, 0.10m), CancellationToken.None);
        await block.ProcessAsync(MakeEdge("balancer", "ARB",  "WETH", 0.0007m, 0m, 0.10m), CancellationToken.None);

        output.Should().NotBeNull("path should be found before reset");

        // Reset and re-process — should need to rebuild graph from scratch
        block.Reset();
        output = null;

        await block.ProcessAsync(MakeEdge("camelot", "WETH", "USDC", 1000m, 0m, 0.10m), CancellationToken.None);
        output.Should().BeNull("single hop after reset should not emit a path");
    }

    [Fact]
    public async Task SpreadCalculatorBlock_PositiveSpread_EmitsOpportunity()
    {
        var block = new SpreadCalculatorBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // Price on camelot: 2010, price on dfyn: 2000 → 50 bps spread
        await block.ProcessAsync(MakePriceUpdate("camelot", "WETH/USDC", 2010f), CancellationToken.None);
        await block.ProcessAsync(MakePriceUpdate("dfyn",    "WETH/USDC", 2000f), CancellationToken.None);

        output.Should().NotBeNull("positive spread should trigger an ArbitrageOpportunity");
        output!.Value.SocketType.Should().Be(BlockSocketType.ArbitrageOpportunity);

        var root = output.Value.Value;
        root.TryGetProperty("spread_bps", out var bpsEl).Should().BeTrue();
        bpsEl.GetSingle().Should().BeGreaterThan(0f);
    }

    [Fact]
    public async Task SpreadCalculatorBlock_NegligibleSpread_DoesNotEmit()
    {
        var block = new SpreadCalculatorBlock();
        var emitted = false;
        block.OutputProduced += (_, _) => { emitted = true; return ValueTask.CompletedTask; };

        // Identical prices — spread is 0 bps
        await block.ProcessAsync(MakePriceUpdate("camelot", "WETH/USDC", 2000f), CancellationToken.None);
        await block.ProcessAsync(MakePriceUpdate("dfyn",    "WETH/USDC", 2000f), CancellationToken.None);

        emitted.Should().BeFalse("zero spread should not emit below the MinSpreadBps threshold");
    }

    [Fact]
    public async Task ProfitGateBlock_ProfitableOpportunity_PassesThrough()
    {
        var block = new ProfitGateBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        var opportunity = new { net_profit = 50.0m, spread_bps = 25f };
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "arb_opportunity", BlockSocketType.ArbitrageOpportunity,
                JsonSerializer.SerializeToElement(opportunity)),
            CancellationToken.None);

        output.Should().NotBeNull("profitable opportunity must pass the gate");
        output!.Value.SocketType.Should().Be(BlockSocketType.ArbitrageOpportunity);
    }

    [Fact]
    public async Task ProfitGateBlock_UnprofitableOpportunity_IsFiltered()
    {
        var block = new ProfitGateBlock();
        var emitted = false;
        block.OutputProduced += (_, _) => { emitted = true; return ValueTask.CompletedTask; };

        var opportunity = new { net_profit = 1.0m }; // below $10 MinProfitUsd default
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "arb_opportunity", BlockSocketType.ArbitrageOpportunity,
                JsonSerializer.SerializeToElement(opportunity)),
            CancellationToken.None);

        emitted.Should().BeFalse("sub-threshold profit must be filtered out");
    }

    // ── DeFiBlock Tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task MorphoSupplyBlock_OnTrigger_EmitsDeFiSignalWithCorrectProtocol()
    {
        var block = new MorphoSupplyBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "indicator_output", BlockSocketType.IndicatorValue,
                JsonSerializer.SerializeToElement(1.0f)),
            CancellationToken.None);

        output.Should().NotBeNull("MorphoSupplyBlock must emit a DeFiSignal on trigger");
        output!.Value.SocketType.Should().Be(BlockSocketType.DeFiSignal);

        var root = output.Value.Value;
        root.ValueKind.Should().Be(JsonValueKind.Object);

        root.TryGetProperty("protocol", out var protEl).Should().BeTrue();
        protEl.GetString().Should().Be("morpho", "protocol must be 'morpho'");

        root.TryGetProperty("action", out var actionEl).Should().BeTrue();
        actionEl.GetString().Should().Be("supply", "action must be 'supply'");

        root.TryGetProperty("asset", out var assetEl).Should().BeTrue();
        assetEl.GetString().Should().NotBeNullOrEmpty("asset must be specified");
    }

    [Fact]
    public async Task MorphoSupplyBlock_LowApy_DoesNotEmit()
    {
        var block = new MorphoSupplyBlock();
        var emitted = false;
        block.OutputProduced += (_, _) => { emitted = true; return ValueTask.CompletedTask; };

        // Trigger with APY 1% (below default MinApy of 3%)
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "indicator_output", BlockSocketType.IndicatorValue,
                JsonSerializer.SerializeToElement(new { apy = 1.0m })),
            CancellationToken.None);

        emitted.Should().BeFalse("supply should not be triggered below minimum APY");
    }

    [Fact]
    public async Task MorphoBorrowBlock_OnSupplySignal_EmitsBorrowSignal()
    {
        var block = new MorphoBorrowBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        var supplySignal = new { protocol = "morpho", action = "supply", asset = "USDC", amount = 1000m };
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "defi_signal", BlockSocketType.DeFiSignal,
                JsonSerializer.SerializeToElement(supplySignal)),
            CancellationToken.None);

        output.Should().NotBeNull("borrow signal must follow supply");
        output!.Value.SocketType.Should().Be(BlockSocketType.DeFiSignal);
        output.Value.Value.TryGetProperty("action", out var act).Should().BeTrue();
        act.GetString().Should().Be("borrow");
    }

    [Fact]
    public async Task LiquidationGuardBlock_LowHealthFactor_EmitsEmergencyClose()
    {
        var block = new LiquidationGuardBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // HF = 1.03 (below default liquidation threshold of 1.05)
        var healthUpdate = new { health_factor = 1.03m, protocol = "morpho", severity = "Critical" };
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "health_update", BlockSocketType.HealthFactorUpdate,
                JsonSerializer.SerializeToElement(healthUpdate)),
            CancellationToken.None);

        output.Should().NotBeNull("emergency close must be triggered below liquidation threshold");
        output!.Value.SocketType.Should().Be(BlockSocketType.DeFiSignal);

        var root = output.Value.Value;
        root.TryGetProperty("action", out var actionEl).Should().BeTrue();
        actionEl.GetString().Should().Be("emergency_close");
    }

    [Fact]
    public async Task LiquidationGuardBlock_HealthyPosition_DoesNotTrigger()
    {
        var block = new LiquidationGuardBlock();
        var emitted = false;
        block.OutputProduced += (_, _) => { emitted = true; return ValueTask.CompletedTask; };

        var healthUpdate = new { health_factor = 1.8m, protocol = "morpho", severity = "Healthy" };
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "health_update", BlockSocketType.HealthFactorUpdate,
                JsonSerializer.SerializeToElement(healthUpdate)),
            CancellationToken.None);

        emitted.Should().BeFalse("healthy position should not trigger emergency close");
    }

    [Fact]
    public async Task LiquidationGuardBlock_Reset_AllowsRetrigger()
    {
        var block = new LiquidationGuardBlock();
        var emitCount = 0;
        block.OutputProduced += (_, _) => { emitCount++; return ValueTask.CompletedTask; };

        var lowHf = new { health_factor = 1.02m };
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "health_update", BlockSocketType.HealthFactorUpdate,
                JsonSerializer.SerializeToElement(lowHf)),
            CancellationToken.None);

        emitCount.Should().Be(1);

        // Second trigger should be suppressed (once triggered = stays triggered)
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "health_update", BlockSocketType.HealthFactorUpdate,
                JsonSerializer.SerializeToElement(lowHf)),
            CancellationToken.None);

        emitCount.Should().Be(1, "second trigger is suppressed until Reset() is called");

        block.Reset();

        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "health_update", BlockSocketType.HealthFactorUpdate,
                JsonSerializer.SerializeToElement(lowHf)),
            CancellationToken.None);

        emitCount.Should().Be(2, "after Reset(), the guard should fire again");
    }

    [Fact]
    public async Task CollateralHealthBlock_EmitsHealthFactorUpdate_WithSeverity()
    {
        var block = new CollateralHealthBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        var defiSignal = new { health_factor = 1.3m, protocol = "morpho" };
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "defi_signal", BlockSocketType.DeFiSignal,
                JsonSerializer.SerializeToElement(defiSignal)),
            CancellationToken.None);

        output.Should().NotBeNull();
        output!.Value.SocketType.Should().Be(BlockSocketType.HealthFactorUpdate);

        var root = output.Value.Value;
        root.TryGetProperty("severity", out var sevEl).Should().BeTrue();
        sevEl.GetString().Should().Be("Warning", "HF 1.3 is below alert threshold 1.5 but above critical 1.15");
    }

    [Fact]
    public async Task BalancerSwapBlock_OnArbitrageOpportunity_EmitsSwapSignal()
    {
        var block = new BalancerSwapBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        var opportunity = new { spread_bps = 15f, net_profit = 50m };
        await block.ProcessAsync(
            new BlockSignal(Guid.NewGuid(), "arb_trigger", BlockSocketType.ArbitrageOpportunity,
                JsonSerializer.SerializeToElement(opportunity)),
            CancellationToken.None);

        output.Should().NotBeNull();
        output!.Value.SocketType.Should().Be(BlockSocketType.DeFiSignal);

        var root = output.Value.Value;
        root.TryGetProperty("protocol", out var protEl).Should().BeTrue();
        protEl.GetString().Should().Be("balancer");
        root.TryGetProperty("action", out var actEl).Should().BeTrue();
        actEl.GetString().Should().Be("swap");
    }

    // ── Registry ──────────────────────────────────────────────────────────────

    [Fact]
    public void BlockRegistry_ContainsAllArbitrageAndDeFiBlocks()
    {
        var registry = new Services.BlockRegistry();
        registry.Register<SpreadCalculatorBlock>("SpreadCalculatorBlock");
        registry.Register<nHOPPathFinderBlock>("nHOPPathFinderBlock");
        registry.Register<FlashLoanBlock>("FlashLoanBlock");
        registry.Register<ProfitGateBlock>("ProfitGateBlock");
        registry.Register<MorphoSupplyBlock>("MorphoSupplyBlock");
        registry.Register<MorphoBorrowBlock>("MorphoBorrowBlock");
        registry.Register<BalancerSwapBlock>("BalancerSwapBlock");
        registry.Register<CollateralHealthBlock>("CollateralHealthBlock");
        registry.Register<YieldOptimizerBlock>("YieldOptimizerBlock");
        registry.Register<LiquidationGuardBlock>("LiquidationGuardBlock");

        var all = registry.GetAll();
        all.Should().HaveCount(10, "all 10 arbitrage + DeFi blocks must be registered");

        registry.GetByKey("nHOPPathFinderBlock").Should().NotBeNull();
        registry.GetByKey("MorphoSupplyBlock").Should().NotBeNull();
        registry.GetByKey("LiquidationGuardBlock").Should().NotBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates an OnChainEvent price update signal for the nHOP graph.</summary>
    private static BlockSignal MakeEdge(
        string exchange, string tokenIn, string tokenOut,
        decimal price, decimal fee, decimal gasUsd) =>
        new(Guid.NewGuid(), "price_update", BlockSocketType.OnChainEvent,
            JsonSerializer.SerializeToElement(new
            {
                exchange,
                tokenIn,
                tokenOut,
                price,
                fee,
                gasUsd,
            }));

    /// <summary>Creates an OnChainEvent signal for SpreadCalculatorBlock.</summary>
    private static BlockSignal MakePriceUpdate(string exchange, string symbol, float price) =>
        new(Guid.NewGuid(), "price_update", BlockSocketType.OnChainEvent,
            JsonSerializer.SerializeToElement(new { exchange, symbol, price }));
}
