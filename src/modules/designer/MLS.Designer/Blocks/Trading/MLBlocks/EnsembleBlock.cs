using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.MLBlocks;

/// <summary>
/// Ensemble block that receives multiple <see cref="BlockSocketType.MLSignal"/> inputs
/// and emits a weighted-vote consensus signal.
/// </summary>
public sealed class EnsembleBlock : BlockBase
{
    // Accumulate votes in the current tick
    private float _buyWeight;
    private float _sellWeight;
    private float _holdWeight;
    private int   _voteCount;

    private readonly BlockParameter<float> _weightAParam = new("WeightA", "Weight A", "Vote weight for input A", 1f, MinValue: 0f, MaxValue: 10f);
    private readonly BlockParameter<float> _weightBParam = new("WeightB", "Weight B", "Vote weight for input B", 1f, MinValue: 0f, MaxValue: 10f);
    private readonly BlockParameter<float> _weightCParam = new("WeightC", "Weight C", "Vote weight for input C", 1f, MinValue: 0f, MaxValue: 10f);
    private readonly BlockParameter<float> _minConfParam = new("MinConfidence", "Min Confidence", "Minimum consensus confidence", 0.6f, MinValue: 0f, MaxValue: 1f, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "EnsembleBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Ensemble";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_weightAParam, _weightBParam, _weightCParam, _minConfParam];

    /// <summary>Initialises a new <see cref="EnsembleBlock"/>.</summary>
    public EnsembleBlock() : base(
        [BlockSocket.Input("ml_input_a", BlockSocketType.MLSignal),
         BlockSocket.Input("ml_input_b", BlockSocketType.MLSignal),
         BlockSocket.Input("ml_input_c", BlockSocketType.MLSignal)],
        [BlockSocket.Output("ml_output", BlockSocketType.MLSignal)]) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _buyWeight  = 0f;
        _sellWeight = 0f;
        _holdWeight = 0f;
        _voteCount  = 0;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.MLSignal)
            return new ValueTask<BlockSignal?>(result: null);

        var weight = signal.SourceSocketName switch
        {
            "ml_input_a" => _weightAParam.DefaultValue,
            "ml_input_b" => _weightBParam.DefaultValue,
            "ml_input_c" => _weightCParam.DefaultValue,
            _            => 1f,
        };

        if (TryExtractDirection(signal.Value, out var direction, out var confidence))
        {
            switch (direction.ToUpperInvariant())
            {
                case "BUY":  _buyWeight  += weight * confidence; break;
                case "SELL": _sellWeight += weight * confidence; break;
                default:     _holdWeight += weight * confidence; break;
            }
            _voteCount++;
        }

        // Emit after all inputs have voted (3 inputs) or after first vote for single-input use
        if (_voteCount < 1)
            return new ValueTask<BlockSignal?>(result: null);

        var total = _buyWeight + _sellWeight + _holdWeight;
        if (total < 1e-7f)
            return new ValueTask<BlockSignal?>(result: null);

        var winDir   = _buyWeight >= _sellWeight && _buyWeight >= _holdWeight ? "BUY"
                     : _sellWeight >= _holdWeight ? "SELL"
                     : "HOLD";
        var winConf  = winDir switch
        {
            "BUY"  => _buyWeight  / total,
            "SELL" => _sellWeight / total,
            _      => _holdWeight / total,
        };

        // Reset for next tick
        Reset();

        if (winConf < _minConfParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);

        var consensusSignal = new { direction = winDir, confidence = winConf, model_name = "ensemble" };
        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "ml_output", BlockSocketType.MLSignal, consensusSignal));
    }

    private static bool TryExtractDirection(JsonElement value, out string direction, out float confidence)
    {
        direction  = "HOLD";
        confidence = 0f;

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (!value.TryGetProperty("direction", out var d)) return false;
        direction = d.GetString() ?? "HOLD";

        if (value.TryGetProperty("confidence", out var c))
            c.TryGetSingle(out confidence);

        return true;
    }
}
