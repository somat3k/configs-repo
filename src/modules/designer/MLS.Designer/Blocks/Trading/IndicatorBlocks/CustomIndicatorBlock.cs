using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.IndicatorBlocks;

/// <summary>
/// Placeholder for Phase 6 — custom Roslyn-compiled indicator block.
/// In Phase 1, it acts as a passthrough for <see cref="BlockSocketType.IndicatorValue"/> signals.
/// </summary>
public sealed class CustomIndicatorBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("candle_input",    BlockSocketType.CandleStream),
        BlockSocket.Input("indicator_input", BlockSocketType.IndicatorValue),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("indicator_output", BlockSocketType.IndicatorValue),
    ];

    private readonly BlockParameter<string> _codeParam =
        new("Code", "C# Code", "Custom indicator C# expression (Phase 6)", "return indicatorValue;");

    /// <inheritdoc/>
    public override string BlockType   => "CustomIndicatorBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Custom Indicator";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_codeParam];

    /// <summary>Initialises a new <see cref="CustomIndicatorBlock"/>.</summary>
    public CustomIndicatorBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        // Phase 1: passthrough for indicator values
        if (signal.SocketType == BlockSocketType.IndicatorValue)
            return new ValueTask<BlockSignal?>(
                EmitObject(BlockId, "indicator_output", BlockSocketType.IndicatorValue, signal.Value));

        return new ValueTask<BlockSignal?>(result: null);
    }
}
