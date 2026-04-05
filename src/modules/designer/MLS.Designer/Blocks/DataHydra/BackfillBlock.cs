using System.Text.Json;
using MLS.Core.Constants;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DataHydra;

/// <summary>
/// Backfill block — triggers a REST-based historical candle backfill request for
/// a configured date range and emits each backfilled candle as a
/// <see cref="BlockSocketType.CandleStream"/> signal on completion.
/// </summary>
/// <remarks>
/// <para>
/// When a <see cref="BlockSocketType.TrainingStatus"/> signal arrives (e.g. a training job
/// requires more historical data), this block emits a <c>DATA_COLLECTION_START</c> envelope
/// via the output socket requesting the Data Layer to backfill the gap.
/// </para>
/// <para>
/// Input:  <see cref="BlockSocketType.TrainingStatus"/> trigger signal. <br/>
/// Output: <see cref="BlockSocketType.CandleStream"/> — historically backfilled candles.
/// </para>
/// </remarks>
public sealed class BackfillBlock : BlockBase
{
    private readonly BlockParameter<string>  _exchangeParam =
        new("Exchange",   "Exchange",   "Exchange identifier for the backfill request",         "hyperliquid");
    private readonly BlockParameter<string>  _symbolParam =
        new("Symbol",     "Symbol",     "Symbol to backfill",                                   "BTC-USDT");
    private readonly BlockParameter<string>  _timeframeParam =
        new("Timeframe",  "Timeframe",  "Candle timeframe for the backfill",                    "1h");
    private readonly BlockParameter<string>  _fromDateParam =
        new("FromDate",   "From Date",  "Backfill start date (ISO-8601 UTC)",                   "2024-01-01T00:00:00Z");
    private readonly BlockParameter<string>  _toDateParam =
        new("ToDate",     "To Date",    "Backfill end date (ISO-8601 UTC)",                     "2024-12-31T23:59:59Z");
    private readonly BlockParameter<int>     _chunkSizeParam =
        new("ChunkSize",  "Chunk Size", "Number of candles per REST request (max 1000)",        1000,
            MinValue: 1, MaxValue: 1000);

    /// <inheritdoc/>
    public override string BlockType   => "BackfillBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Backfill";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_exchangeParam, _symbolParam, _timeframeParam, _fromDateParam, _toDateParam, _chunkSizeParam];

    /// <summary>Initialises a <see cref="BackfillBlock"/>.</summary>
    public BackfillBlock() : base(
        [BlockSocket.Input("trigger_input",   BlockSocketType.TrainingStatus)],
        [BlockSocket.Output("candle_output",  BlockSocketType.CandleStream)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        // Only trigger on TrainingStatus signals (e.g. from GapMonitor or training lifecycle)
        if (signal.SocketType != BlockSocketType.TrainingStatus)
            return new ValueTask<BlockSignal?>(result: null);

        if (signal.SocketType != BlockSocketType.TrainingStatus)
        {
            return new ValueTask<BlockSignal?>((BlockSignal?)null);
        }

        // Emit a DATA_COLLECTION_START request payload describing the backfill job
        var request = new
        {
            type         = MessageTypes.DataCollectionStart,
            exchange     = _exchangeParam.DefaultValue,
            symbol       = _symbolParam.DefaultValue,
            timeframe    = _timeframeParam.DefaultValue,
            from         = _fromDateParam.DefaultValue,
            to           = _toDateParam.DefaultValue,
            chunk_size   = _chunkSizeParam.DefaultValue,
            mode         = "backfill",
            requested_at = DateTimeOffset.UtcNow,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "candle_output", BlockSocketType.CandleStream, request));
    }
}
