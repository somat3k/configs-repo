using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.MLTraining;

/// <summary>
/// Validate model block — inspects incoming <see cref="BlockSocketType.TrainingStatus"/>
/// signals from <c>TrainModelBlock</c> and classifies the trained model as
/// <c>ACCEPTED</c> or <c>REJECTED</c> based on configurable metric thresholds.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Progress updates (state = <c>TRAINING</c>) are passed through unchanged.</item>
///   <item>Completion updates (state = <c>COMPLETE</c>) are validated against
///         <c>MinAccuracy</c>, <c>MaxValLoss</c>, and <c>MinF1Score</c>.
///         The signal is re-emitted with <c>state</c> set to <c>ACCEPTED</c> or
///         <c>REJECTED</c> and a <c>validation</c> object appended.</item>
/// </list>
/// </remarks>
public sealed class ValidateModelBlock : BlockBase
{
    private readonly BlockParameter<float> _minAccuracyParam =
        new("MinAccuracy",  "Min Accuracy",  "Minimum validation accuracy to accept model",  0.60f, MinValue: 0f, MaxValue: 1f, IsOptimizable: false);
    private readonly BlockParameter<float> _maxValLossParam =
        new("MaxValLoss",   "Max Val Loss",  "Maximum validation loss to accept model",       0.50f, MinValue: 0f, MaxValue: 10f, IsOptimizable: false);
    private readonly BlockParameter<float> _minF1ScoreParam =
        new("MinF1Score",   "Min F1 Score",  "Minimum macro-F1 score to accept model",       0.50f, MinValue: 0f, MaxValue: 1f,  IsOptimizable: false);

    /// <inheritdoc/>
    public override string BlockType   => "ValidateModelBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Validate Model";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_minAccuracyParam, _maxValLossParam, _minF1ScoreParam];

    /// <summary>Initialises a new <see cref="ValidateModelBlock"/>.</summary>
    public ValidateModelBlock() : base(
        [BlockSocket.Input("status_input",  BlockSocketType.TrainingStatus)],
        [BlockSocket.Output("status_output", BlockSocketType.TrainingStatus)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.TrainingStatus)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractStatus(signal.Value, out var state, out var accuracy, out var valLoss, out var f1))
            return new ValueTask<BlockSignal?>(result: null);

        // Pass PENDING / TRAINING states through unchanged
        if (!string.Equals(state, "COMPLETE", StringComparison.OrdinalIgnoreCase))
            return new ValueTask<BlockSignal?>(result: signal);

        // ── Validate completed model ──────────────────────────────────────────────
        bool accOk = accuracy   >= _minAccuracyParam.DefaultValue;
        bool lossOk = valLoss   <= _maxValLossParam.DefaultValue;
        bool f1Ok   = f1        >= _minF1ScoreParam.DefaultValue;
        bool passed = accOk && lossOk && f1Ok;

        var reason = passed
            ? "All validation thresholds met"
            : BuildRejectionReason(accOk, lossOk, f1Ok, accuracy, valLoss, f1);

        // Re-emit the original signal value extended with validation info
        var extended = ExtendWithValidation(signal.Value, passed ? "ACCEPTED" : "REJECTED", passed, reason);

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "status_output", BlockSocketType.TrainingStatus, extended));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private string BuildRejectionReason(
        bool accOk, bool lossOk, bool f1Ok,
        float accuracy, float valLoss, float f1)
    {
        var reasons = new List<string>();
        if (!accOk)   reasons.Add($"accuracy {accuracy:F4} < min {_minAccuracyParam.DefaultValue:F4}");
        if (!lossOk)  reasons.Add($"val_loss {valLoss:F4} > max {_maxValLossParam.DefaultValue:F4}");
        if (!f1Ok)    reasons.Add($"f1_score {f1:F4} < min {_minF1ScoreParam.DefaultValue:F4}");
        return string.Join("; ", reasons);
    }

    private static object ExtendWithValidation(JsonElement original, string newState, bool passed, string reason)
    {
        // Build a dynamic object that copies all fields from original and appends validation
        using var doc = JsonDocument.Parse(original.GetRawText());
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name == "state") continue;  // Will be overwritten
            fields[prop.Name] = prop.Value.Clone();
        }

        fields["state"] = newState;
        fields["validation"] = new
        {
            passed,
            reason,
            timestamp = DateTimeOffset.UtcNow,
        };

        return fields;
    }

    private static bool TryExtractStatus(
        JsonElement value,
        out string state, out float accuracy, out float valLoss, out float f1)
    {
        state    = string.Empty;
        accuracy = 0f;
        valLoss  = float.MaxValue;
        f1       = 0f;

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (!value.TryGetProperty("state", out var stEl)) return false;
        state = stEl.GetString() ?? string.Empty;

        bool hasAccuracy = value.TryGetProperty("accuracy", out var acc) && acc.TryGetSingle(out accuracy);
        bool hasValLoss  = value.TryGetProperty("val_loss",  out var vl)  && vl.TryGetSingle(out valLoss);
        bool hasF1       = false;

        // Also check top-level f1_macro
        if (value.TryGetProperty("f1_macro", out var f1TopEl)) hasF1 = f1TopEl.TryGetSingle(out f1);

        // Fallback: read from nested "metrics" object for any value not found at top level
        if (value.TryGetProperty("metrics", out var metricsEl) &&
            metricsEl.ValueKind == JsonValueKind.Object)
        {
            if (!hasAccuracy &&
                metricsEl.TryGetProperty("accuracy", out var mAcc) &&
                mAcc.TryGetSingle(out var mAccVal))
            {
                accuracy    = mAccVal;
                hasAccuracy = true;
            }

            if (!hasValLoss &&
                metricsEl.TryGetProperty("val_loss", out var mVl) &&
                mVl.TryGetSingle(out var mVlVal))
            {
                valLoss    = mVlVal;
                hasValLoss = true;
            }

            if (!hasF1 &&
                metricsEl.TryGetProperty("f1_macro", out var mF1) &&
                mF1.TryGetSingle(out var mF1Val))
            {
                f1    = mF1Val;
                hasF1 = true;
            }
        }

        return true;
    }
}
