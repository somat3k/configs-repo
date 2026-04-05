using System.Text.Json;
using MLS.Core.Constants;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DataHydra;

/// <summary>
/// Gap monitor block — runs a periodic check on an incoming candle stream to detect
/// data gaps (missing candles) and emits a <c>DATA_GAP_DETECTED</c> signal when the
/// actual candle count falls below the expected count by more than the configured tolerance.
/// </summary>
/// <remarks>
/// <para>
/// The block counts received candles per rolling window and compares against the expected
/// count derived from the configured timeframe.  When the gap tolerance is breached, a
/// gap-detected signal is emitted so downstream blocks (e.g. <see cref="BackfillBlock"/>)
/// can initiate a backfill.
/// </para>
/// <para>
/// Input:  <see cref="BlockSocketType.CandleStream"/>. <br/>
/// Output: <see cref="BlockSocketType.CandleStream"/> carrying a <c>DATA_GAP_DETECTED</c> alert.
/// </para>
/// </remarks>
public sealed class GapMonitorBlock : BlockBase
{
    private readonly BlockParameter<string>  _exchangeParam =
        new("Exchange",           "Exchange",           "Exchange to monitor",                           "hyperliquid");
    private readonly BlockParameter<string>  _symbolParam =
        new("Symbol",             "Symbol",             "Symbol to monitor",                             "BTC-USDT");
    private readonly BlockParameter<string>  _timeframeParam =
        new("Timeframe",          "Timeframe",          "Expected candle timeframe (e.g. 1m, 5m, 1h)",  "1m");
    private readonly BlockParameter<double>  _toleranceParam =
        new("GapTolerancePct",    "Gap Tolerance %",    "Gap threshold (0.05 = 5% missing triggers alert)", 0.05,
            MinValue: 0.001, MaxValue: 1.0);
    private readonly BlockParameter<int>     _windowMinutesParam =
        new("WindowMinutes",      "Window (minutes)",   "Rolling window size in minutes for gap detection", 60,
            MinValue: 1, MaxValue: 1440);

    // ── Rolling window state ──────────────────────────────────────────────────────
    private int _receivedCount;
    private DateTimeOffset _windowStart = DateTimeOffset.MinValue;

    /// <inheritdoc/>
    public override string BlockType   => "GapMonitorBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Gap Monitor";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_exchangeParam, _symbolParam, _timeframeParam, _toleranceParam, _windowMinutesParam];

    /// <summary>Initialises a <see cref="GapMonitorBlock"/>.</summary>
    public GapMonitorBlock() : base(
        [BlockSocket.Input("candle_input",  BlockSocketType.CandleStream)],
        [BlockSocket.Output("gap_output",   BlockSocketType.CandleStream)]) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _receivedCount = 0;
        _windowStart   = DateTimeOffset.MinValue;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        var now = DateTimeOffset.UtcNow;

        // Initialise the rolling window on first signal
        if (_windowStart == DateTimeOffset.MinValue)
            _windowStart = now;

        _receivedCount++;

        var windowDuration = now - _windowStart;
        var windowMinutes  = _windowMinutesParam.DefaultValue;

        if (windowDuration.TotalMinutes < windowMinutes)
            return new ValueTask<BlockSignal?>(result: null);  // Window not yet full

        // Calculate expected candle count from timeframe
        var timeframe = _timeframeParam.DefaultValue;
        int expectedCount = ComputeExpected(timeframe, windowMinutes);

        double gapRatio = expectedCount > 0
            ? 1.0 - (double)_receivedCount / expectedCount
            : 0.0;

        // Capture locals before resetting window state
        var priorWindowStart  = _windowStart;
        var priorReceivedCount = _receivedCount;
        int missingCount = Math.Max(0, expectedCount - priorReceivedCount);

        // Roll the window
        _windowStart   = now;
        _receivedCount = 0;

        if (gapRatio <= _toleranceParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);  // No gap

        // Gap detected — emit DATA_GAP_DETECTED signal
        var alert = new
        {
            type               = MessageTypes.DataGapDetected,
            exchange           = _exchangeParam.DefaultValue,
            symbol             = _symbolParam.DefaultValue,
            timeframe,
            expected_count     = expectedCount,
            received_count     = priorReceivedCount,
            missing_count      = missingCount,
            gap_ratio          = Math.Round(gapRatio, 4),
            window_start       = priorWindowStart,
            window_end         = now,
            detected_at        = now,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "gap_output", BlockSocketType.CandleStream, alert));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static int ComputeExpected(string timeframe, int windowMinutes) => timeframe switch
    {
        "1m"  => windowMinutes,
        "3m"  => windowMinutes / 3,
        "5m"  => windowMinutes / 5,
        "15m" => windowMinutes / 15,
        "30m" => windowMinutes / 30,
        "1h"  => windowMinutes / 60,
        "4h"  => windowMinutes / 240,
        "1d"  => windowMinutes / 1440,
        _     => windowMinutes,  // Default: treat as 1m
    };
}
