using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.RiskBlocks;

/// <summary>
/// Exposure limit block that caps total portfolio exposure.
/// Passes signals through when total exposure is below the configured limit;
/// denies them otherwise.
/// </summary>
public sealed class ExposureLimitBlock : BlockBase
{
    private float _totalExposureUsd;

    private readonly BlockParameter<float> _maxExposureParam = new("MaxExposureUsd", "Max Exposure (USD)", "Total portfolio exposure cap", 100_000f, MinValue: 1000f, MaxValue: 10_000_000f);

    /// <inheritdoc/>
    public override string BlockType   => "ExposureLimitBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Exposure Limit";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_maxExposureParam];

    /// <summary>Initialises a new <see cref="ExposureLimitBlock"/>.</summary>
    public ExposureLimitBlock() : base(
        [BlockSocket.Input("signal_input", BlockSocketType.MLSignal),
         BlockSocket.Input("fill_input",   BlockSocketType.OrderResult)],
        [BlockSocket.Output("risk_output", BlockSocketType.RiskDecision)]) { }

    /// <inheritdoc/>
    public override void Reset() => _totalExposureUsd = 0f;

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        // Handle fill updates
        if (signal.SocketType == BlockSocketType.OrderResult)
        {
            if (signal.Value.ValueKind == JsonValueKind.Object
                && signal.Value.TryGetProperty("status", out var st) && st.GetString() == "filled"
                && signal.Value.TryGetProperty("size_usd", out var sz) && sz.TryGetSingle(out var filledSize))
            {
                _totalExposureUsd += filledSize;
            }
            return new ValueTask<BlockSignal?>(result: null);
        }

        if (signal.SocketType != BlockSocketType.MLSignal)
            return new ValueTask<BlockSignal?>(result: null);

        TryExtract(signal.Value, out var direction, out var confidence);

        if (_totalExposureUsd >= _maxExposureParam.DefaultValue)
        {
            var deny = new { allow = false, reason = "exposure_limit_reached", current_exposure = _totalExposureUsd, max_exposure = _maxExposureParam.DefaultValue };
            return new ValueTask<BlockSignal?>(EmitObject(BlockId, "risk_output", BlockSocketType.RiskDecision, deny));
        }

        var allow = new { allow = true, direction, confidence };
        return new ValueTask<BlockSignal?>(EmitObject(BlockId, "risk_output", BlockSocketType.RiskDecision, allow));
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
