using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.IndicatorBlocks;

/// <summary>
/// Volume Profile indicator block.
/// Accumulates volume at price in a configurable number of bins.
/// Emits the current bar's volume percentile (0 = low, 1 = high) on
/// <see cref="BlockSocketType.IndicatorValue"/>.
/// </summary>
public sealed class VolumeProfileBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("candle_input", BlockSocketType.CandleStream),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("indicator_output", BlockSocketType.IndicatorValue),
    ];

    private readonly float[] _volumeHistory;
    private int   _head;
    private int   _count;

    private readonly BlockParameter<int> _lookbackParam =
        new("Lookback", "Lookback", "Number of bars for volume percentile", 50, MinValue: 5, MaxValue: 500);

    /// <inheritdoc/>
    public override string BlockType   => "VolumeProfileBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Volume Profile";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_lookbackParam];

    /// <summary>Initialises a new <see cref="VolumeProfileBlock"/>.</summary>
    public VolumeProfileBlock() : base(_inputs, _outputs)
    {
        _volumeHistory = new float[_lookbackParam.DefaultValue];
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        Array.Clear(_volumeHistory, 0, _volumeHistory.Length);
        _head  = 0;
        _count = 0;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractVolume(signal.Value, out var volume))
            return new ValueTask<BlockSignal?>(result: null);

        _volumeHistory[_head] = volume;
        _head = (_head + 1) % _volumeHistory.Length;
        if (_count < _volumeHistory.Length) _count++;

        if (_count < 2)
            return new ValueTask<BlockSignal?>(result: null);

        // Compute volume percentile of the current bar
        var sorted  = _volumeHistory[.._count];
        Array.Sort(sorted);
        var rank    = Array.BinarySearch(sorted, volume);
        if (rank < 0) rank = ~rank;
        var percentile = (float)rank / _count;

        return new ValueTask<BlockSignal?>(
            EmitFloat(BlockId, "indicator_output", BlockSocketType.IndicatorValue, percentile));
    }

    private static bool TryExtractVolume(JsonElement value, out float volume)
    {
        volume = 0f;
        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("volume", out var v)
            && v.TryGetSingle(out volume))
            return true;
        return false;
    }
}
