using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.RiskBlocks;

/// <summary>
/// Position sizer block using configurable fixed-fraction or Kelly criterion.
/// Receives a <see cref="BlockSocketType.MLSignal"/> and emits a
/// <see cref="BlockSocketType.RiskDecision"/> with sizing recommendation.
/// </summary>
public sealed class PositionSizerBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("signal_input", BlockSocketType.MLSignal),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("risk_output", BlockSocketType.RiskDecision),
    ];

    private readonly BlockParameter<float>  _riskFractionParam = new("RiskFraction",  "Risk Fraction", "Portfolio fraction per trade",  0.01f, MinValue: 0.001f, MaxValue: 0.25f, IsOptimizable: true);
    private readonly BlockParameter<float>  _maxPositionParam  = new("MaxPosition",   "Max Position",  "Maximum position size (USD)",    10000f, MinValue: 100f, MaxValue: 1_000_000f);
    private readonly BlockParameter<string> _methodParam       = new("Method",        "Method",        "Sizing: FixedFraction | Kelly",  "FixedFraction");

    /// <inheritdoc/>
    public override string BlockType   => "PositionSizerBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Position Sizer";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_riskFractionParam, _maxPositionParam, _methodParam];

    /// <summary>Initialises a new <see cref="PositionSizerBlock"/>.</summary>
    public PositionSizerBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.MLSignal)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtract(signal.Value, out var direction, out var confidence))
            return new ValueTask<BlockSignal?>(result: null);

        var sizeUsd = _methodParam.DefaultValue == "Kelly"
            ? CalculateKelly(confidence) * _maxPositionParam.DefaultValue
            : _riskFractionParam.DefaultValue * _maxPositionParam.DefaultValue;

        sizeUsd = Math.Min(sizeUsd, _maxPositionParam.DefaultValue);

        var decision = new
        {
            allow      = true,
            direction,
            size_usd   = sizeUsd,
            confidence,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "risk_output", BlockSocketType.RiskDecision, decision));
    }

    private static float CalculateKelly(float confidence)
    {
        // Kelly fraction = 2p − 1 (simplified, assuming win = 1, loss = 1)
        return Math.Max(2f * confidence - 1f, 0f);
    }

    private static bool TryExtract(JsonElement value, out string direction, out float confidence)
    {
        direction  = "HOLD";
        confidence = 0f;
        if (value.ValueKind != JsonValueKind.Object) return false;
        if (!value.TryGetProperty("direction", out var d)) return false;
        direction = d.GetString() ?? "HOLD";
        if (value.TryGetProperty("confidence", out var c)) c.TryGetSingle(out confidence);
        return true;
    }
}
