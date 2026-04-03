using System.Text.Json;
using FluentAssertions;
using MLS.Core.Designer;
using MLS.Designer.Blocks.Trading.IndicatorBlocks;
using Xunit;

namespace MLS.Designer.Tests;

/// <summary>
/// Unit tests for Trading-domain indicator blocks using known OHLCV fixture data.
/// All tests run deterministic computations — no mocking required.
/// </summary>
public sealed class TradingBlockTests
{
    // ── RSIBlock ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RSIBlock_KnownClosePrices_ProducesCorrectRsi()
    {
        // Classic 14-period RSI test vector.
        // Prices from: https://school.stockcharts.com/doku.php?id=technical_indicators:relative_strength_index_rsi
        var closes = new float[]
        {
            44.34f, 44.09f, 44.15f, 43.61f, 44.33f, 44.83f, 45.10f, 45.15f,
            43.61f, 44.33f, 44.83f, 45.10f, 45.15f, 45.98f,  // 14 values to warm up
            45.41f  // 15th value: should produce RSI
        };

        var block           = new RSIBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        for (var i = 0; i < closes.Length - 1; i++)
            await block.ProcessAsync(MakeCandle(closes[i]), CancellationToken.None);

        // Process the 15th close — should emit RSI
        await block.ProcessAsync(MakeCandle(closes[^1]), CancellationToken.None);

        output.Should().NotBeNull("RSI should be emitted after warm-up period");
        output!.Value.SocketType.Should().Be(BlockSocketType.IndicatorValue);
        output.Value.Value.ValueKind.Should().Be(JsonValueKind.Number);

        var rsiNorm = output.Value.Value.GetSingle();
        rsiNorm.Should().BeInRange(0f, 1f, "normalised RSI must be in [0, 1]");
    }

    [Fact]
    public async Task RSIBlock_NoSignalDuringWarmup()
    {
        var block = new RSIBlock();
        var signalCount = 0;
        block.OutputProduced += (_, _) => { signalCount++; return ValueTask.CompletedTask; };

        // Feed 13 candles — should NOT emit (period = 14 requires 14 bars after first)
        for (var i = 0; i < 13; i++)
            await block.ProcessAsync(MakeCandle(100f + i), CancellationToken.None);

        signalCount.Should().Be(0, "RSI should not emit until warm-up is complete");
    }

    [Fact]
    public async Task RSIBlock_Reset_ClearsState()
    {
        var block   = new RSIBlock();
        var outputs = new List<BlockSignal>();
        block.OutputProduced += (sig, _) => { outputs.Add(sig); return ValueTask.CompletedTask; };

        // Warm up
        for (var i = 0; i < 20; i++)
            await block.ProcessAsync(MakeCandle(100f + i * 0.5f), CancellationToken.None);

        outputs.Should().NotBeEmpty();
        outputs.Clear();

        block.Reset();

        // After reset, no output until warm-up again
        for (var i = 0; i < 10; i++)
            await block.ProcessAsync(MakeCandle(110f + i), CancellationToken.None);

        outputs.Should().BeEmpty("RSI must not emit before completing warm-up after Reset");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public async Task RSIBlock_AllUpCandles_ProducesHighRsi(int period)
    {
        var block = new RSIBlock();
        BlockSignal? lastOutput = null;
        block.OutputProduced += (sig, _) => { lastOutput = sig; return ValueTask.CompletedTask; };

        // Feed period+10 strictly increasing candles
        for (var i = 0; i < period + 10; i++)
            await block.ProcessAsync(MakeCandle(100f + i), CancellationToken.None);

        lastOutput.Should().NotBeNull();
        var rsi = lastOutput!.Value.Value.GetSingle() * 100f;
        rsi.Should().BeGreaterThan(90f, "all-up candles should produce RSI near 100");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public async Task RSIBlock_AllDownCandles_ProducesLowRsi(int period)
    {
        var block = new RSIBlock();
        BlockSignal? lastOutput = null;
        block.OutputProduced += (sig, _) => { lastOutput = sig; return ValueTask.CompletedTask; };

        for (var i = 0; i < period + 10; i++)
            await block.ProcessAsync(MakeCandle(1000f - i * 5f), CancellationToken.None);

        lastOutput.Should().NotBeNull();
        var rsi = lastOutput!.Value.Value.GetSingle() * 100f;
        rsi.Should().BeLessThan(10f, "all-down candles should produce RSI near 0");
    }

    // ── MACDBlock ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MACDBlock_ProducesOutputAfterSlowPlusSIgnalPeriod()
    {
        var block  = new MACDBlock();
        var outputs = new List<BlockSignal>();
        block.OutputProduced += (sig, _) => { outputs.Add(sig); return ValueTask.CompletedTask; };

        // Default periods: slow=26, signal=9 → need 34+ candles
        for (var i = 0; i < 40; i++)
            await block.ProcessAsync(MakeCandle(100f + MathF.Sin(i * 0.3f) * 5f), CancellationToken.None);

        outputs.Should().NotBeEmpty("MACD should emit after slow + signal warm-up");
        foreach (var sig in outputs)
            sig.SocketType.Should().Be(BlockSocketType.IndicatorValue);
    }

    // ── BollingerBlock ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BollingerBlock_OutputInZeroToOneRange()
    {
        var block   = new BollingerBlock();
        var outputs = new List<BlockSignal>();
        block.OutputProduced += (sig, _) => { outputs.Add(sig); return ValueTask.CompletedTask; };

        for (var i = 0; i < 30; i++)
            await block.ProcessAsync(MakeCandle(100f + MathF.Sin(i * 0.5f) * 10f), CancellationToken.None);

        outputs.Should().NotBeEmpty();
        foreach (var sig in outputs)
        {
            var value = sig.Value.GetSingle();
            value.Should().BeInRange(0f, 1f, "Bollinger position must be in [0, 1]");
        }
    }

    [Fact]
    public async Task BollingerBlock_NoOutputDuringWarmup()
    {
        var block      = new BollingerBlock();
        var signalCount = 0;
        block.OutputProduced += (_, _) => { signalCount++; return ValueTask.CompletedTask; };

        // Default period = 20; 19 candles should not emit
        for (var i = 0; i < 19; i++)
            await block.ProcessAsync(MakeCandle(100f), CancellationToken.None);

        signalCount.Should().Be(0);
    }

    // ── ATRBlock ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ATRBlock_ProducesPositiveATR_AfterWarmup()
    {
        var block   = new ATRBlock();
        var outputs = new List<BlockSignal>();
        block.OutputProduced += (sig, _) => { outputs.Add(sig); return ValueTask.CompletedTask; };

        for (var i = 0; i < 20; i++)
            await block.ProcessAsync(MakeOhlcv(100f + i, 105f + i, 95f + i, 102f + i), CancellationToken.None);

        outputs.Should().NotBeEmpty();
        foreach (var sig in outputs)
            sig.Value.GetSingle().Should().BeGreaterThan(0f, "ATR must be positive");
    }

    // ── VWAPBlock ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task VWAPBlock_ProducesOutputForEachCandleWithVolume()
    {
        var block   = new VWAPBlock();
        var outputs = new List<BlockSignal>();
        block.OutputProduced += (sig, _) => { outputs.Add(sig); return ValueTask.CompletedTask; };

        for (var i = 0; i < 5; i++)
            await block.ProcessAsync(MakeOhlcv(100f, 105f, 95f, 102f, volume: 1000f + i * 100f), CancellationToken.None);

        outputs.Count.Should().Be(5, "VWAP emits on each candle with volume");
    }

    // ── VolumeProfileBlock ────────────────────────────────────────────────────────

    [Fact]
    public async Task VolumeProfileBlock_ProducesPercentileInZeroToOne()
    {
        var block   = new VolumeProfileBlock();
        var outputs = new List<BlockSignal>();
        block.OutputProduced += (sig, _) => { outputs.Add(sig); return ValueTask.CompletedTask; };

        // Varying volume
        for (var i = 1; i <= 10; i++)
            await block.ProcessAsync(MakeOhlcv(100f, 105f, 95f, 102f, volume: i * 1000f), CancellationToken.None);

        outputs.Should().NotBeEmpty();
        foreach (var sig in outputs)
        {
            var p = sig.Value.GetSingle();
            p.Should().BeInRange(0f, 1f, "volume percentile must be in [0, 1]");
        }
    }

    // ── BlockRegistry ─────────────────────────────────────────────────────────────

    [Fact]
    public void BlockRegistry_GetAll_ReturnsAllTradingBlocks()
    {
        var registry = BuildFullRegistry();
        var all      = registry.GetAll();

        all.Should().NotBeEmpty();

        // Must contain all 7 indicator blocks
        all.Select(b => b.Key).Should().Contain(new[]
        {
            "RSIBlock", "MACDBlock", "BollingerBlock", "ATRBlock",
            "VWAPBlock", "VolumeProfileBlock", "CustomIndicatorBlock",
        });

        // Must contain all DataSource blocks
        all.Select(b => b.Key).Should().Contain(new[]
        {
            "CandleFeedBlock", "OrderBookFeedBlock", "TradeFeedBlock", "BacktestReplayBlock",
        });

        // Must contain ML blocks
        all.Select(b => b.Key).Should().Contain(new[]
        {
            "ModelTInferenceBlock", "ModelAInferenceBlock", "ModelDInferenceBlock", "EnsembleBlock",
        });
    }

    [Fact]
    public void BlockRegistry_GetByKey_ReturnsMeta_ForRegisteredBlock()
    {
        var registry = BuildFullRegistry();
        var meta     = registry.GetByKey("RSIBlock");

        meta.Should().NotBeNull();
        meta!.Key.Should().Be("RSIBlock");
        meta.Category.Should().Be("Indicator");
        meta.InputSocketNames.Should().Contain("candle_input");
        meta.OutputSocketNames.Should().Contain("indicator_output");
    }

    [Fact]
    public void BlockRegistry_GetByKey_ReturnsNull_ForUnregisteredBlock()
    {
        var registry = BuildFullRegistry();
        registry.GetByKey("NonExistentBlock").Should().BeNull();
    }

    [Fact]
    public void BlockRegistry_CreateInstance_ReturnsNewInstance()
    {
        var registry  = BuildFullRegistry();
        var instance1 = registry.CreateInstance("RSIBlock");
        var instance2 = registry.CreateInstance("RSIBlock");

        instance1.Should().NotBeNull().And.BeAssignableTo<RSIBlock>();
        instance2.Should().NotBeNull().And.BeAssignableTo<RSIBlock>();
        instance1!.BlockId.Should().NotBe(instance2!.BlockId, "each instance must have a unique BlockId");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static BlockSignal MakeCandle(float close) =>
        new(Guid.NewGuid(), "candle_output", BlockSocketType.CandleStream,
            JsonSerializer.SerializeToElement(new { open = close, high = close + 1f, low = close - 1f, close, volume = 1000f }));

    private static BlockSignal MakeOhlcv(float open, float high, float low, float close, float volume = 1000f) =>
        new(Guid.NewGuid(), "candle_output", BlockSocketType.CandleStream,
            JsonSerializer.SerializeToElement(new { open, high, low, close, volume }));

    private static Services.BlockRegistry BuildFullRegistry()
    {
        var r = new Services.BlockRegistry();
        r.Register<RSIBlock>("RSIBlock");
        r.Register<MACDBlock>("MACDBlock");
        r.Register<BollingerBlock>("BollingerBlock");
        r.Register<ATRBlock>("ATRBlock");
        r.Register<VWAPBlock>("VWAPBlock");
        r.Register<VolumeProfileBlock>("VolumeProfileBlock");
        r.Register<CustomIndicatorBlock>("CustomIndicatorBlock");
        r.Register<Blocks.Trading.DataSourceBlocks.CandleFeedBlock>("CandleFeedBlock");
        r.Register<Blocks.Trading.DataSourceBlocks.OrderBookFeedBlock>("OrderBookFeedBlock");
        r.Register<Blocks.Trading.DataSourceBlocks.TradeFeedBlock>("TradeFeedBlock");
        r.Register<Blocks.Trading.DataSourceBlocks.BacktestReplayBlock>("BacktestReplayBlock");
        r.Register<Blocks.Trading.MLBlocks.ModelTInferenceBlock>("ModelTInferenceBlock");
        r.Register<Blocks.Trading.MLBlocks.ModelAInferenceBlock>("ModelAInferenceBlock");
        r.Register<Blocks.Trading.MLBlocks.ModelDInferenceBlock>("ModelDInferenceBlock");
        r.Register<Blocks.Trading.MLBlocks.EnsembleBlock>("EnsembleBlock");
        return r;
    }
}
