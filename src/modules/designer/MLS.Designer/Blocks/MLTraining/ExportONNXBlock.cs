using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Constants;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.MLTraining;

/// <summary>
/// Export ONNX block — monitors incoming <see cref="BlockSocketType.TrainingStatus"/> signals
/// and, when it detects a model in <c>ACCEPTED</c> state, confirms the ONNX and JOBLIB
/// artefacts produced by Shell VM and optionally triggers IPFS upload notification.
/// </summary>
/// <remarks>
/// <para>
/// The Shell VM training pipeline (<c>training_pipeline.py</c>) automatically exports
/// ONNX and JOBLIB artefacts as part of the <c>TRAINING_JOB_COMPLETE</c> lifecycle step.
/// The paths and IPFS CID are embedded in the <c>COMPLETE</c> status by <c>TrainModelBlock</c>.
/// This block forwards the export details downstream so the canvas can display the results,
/// and emits a final <c>EXPORT_READY</c> status.
/// </para>
/// <para>
/// Signals in states other than <c>ACCEPTED</c> / <c>COMPLETE</c> are passed through unchanged.
/// </para>
/// </remarks>
public sealed class ExportONNXBlock : BlockBase
{
    private readonly BlockParameter<bool>   _requireIpfsParam =
        new("RequireIpfs", "Require IPFS Upload",
            "Only emit EXPORT_READY when an IPFS CID is present", true);
    private readonly BlockParameter<string> _versionTagParam =
        new("VersionTag",  "Version Tag",
            "Optional suffix appended to the model version string (e.g. 'staging')", string.Empty);

    /// <inheritdoc/>
    public override string BlockType   => "ExportONNXBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Export ONNX";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_requireIpfsParam, _versionTagParam];

    /// <summary>Initialises a new <see cref="ExportONNXBlock"/>.</summary>
    public ExportONNXBlock() : base(
        [BlockSocket.Input("status_input",  BlockSocketType.TrainingStatus)],
        [BlockSocket.Output("export_output", BlockSocketType.TrainingStatus)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.TrainingStatus)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractStatus(signal.Value,
                out var state, out var jobId, out var modelType, out var modelId,
                out var onnxPath, out var joblibPath, out var ipfsCid, out var metrics))
            return new ValueTask<BlockSignal?>(result: null);

        // Pass non-terminal states through without modification
        if (!IsExportableState(state))
            return new ValueTask<BlockSignal?>(result: signal);

        // If IPFS upload is required but CID is absent, pass through as-is
        if (_requireIpfsParam.DefaultValue && string.IsNullOrWhiteSpace(ipfsCid))
            return new ValueTask<BlockSignal?>(result: signal);

        var versionTag  = _versionTagParam.DefaultValue;
        var effectiveId = string.IsNullOrWhiteSpace(versionTag)
            ? modelId
            : $"{modelId}-{versionTag.Trim()}";

        var exportStatus = new ExportReadyStatus(
            JobId:      jobId,
            ModelType:  modelType,
            State:      TrainingState.ExportReady,
            ModelId:    effectiveId,
            OnnxPath:   onnxPath   ?? string.Empty,
            JoblibPath: joblibPath ?? string.Empty,
            IpfsCid:    ipfsCid ?? string.Empty,
            Metrics:    metrics,
            ExportedAt: DateTimeOffset.UtcNow);

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "export_output", BlockSocketType.TrainingStatus, exportStatus));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static bool IsExportableState(string state) =>
        string.Equals(state, TrainingState.Accepted, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(state, TrainingState.Complete, StringComparison.OrdinalIgnoreCase);

    private static bool TryExtractStatus(
        JsonElement value,
        out string   state,
        out string   jobId,
        out string   modelType,
        out string   modelId,
        out string?  onnxPath,
        out string?  joblibPath,
        out string?  ipfsCid,
        out JsonElement? metrics)
    {
        state      = string.Empty;
        jobId      = string.Empty;
        modelType  = string.Empty;
        modelId    = string.Empty;
        onnxPath   = null;
        joblibPath = null;
        ipfsCid    = null;
        metrics    = null;

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (!value.TryGetProperty("state", out var stEl)) return false;
        state = stEl.GetString() ?? string.Empty;

        if (value.TryGetProperty("job_id",     out var ji)) jobId     = ji.GetString() ?? string.Empty;
        if (value.TryGetProperty("model_type", out var mt)) modelType = mt.GetString() ?? string.Empty;
        if (value.TryGetProperty("model_id",   out var mi)) modelId   = mi.GetString() ?? string.Empty;
        if (value.TryGetProperty("onnx_path",  out var op)) onnxPath  = op.GetString();
        if (value.TryGetProperty("joblib_path",out var jp)) joblibPath = jp.GetString();
        if (value.TryGetProperty("ipfs_cid",   out var ic)) ipfsCid   = ic.GetString();

        if (value.TryGetProperty("metrics", out var m) && m.ValueKind == JsonValueKind.Object)
            metrics = m;

        return true;
    }

    // ── Wire types ────────────────────────────────────────────────────────────────

    internal sealed record ExportReadyStatus(
        [property: JsonPropertyName("job_id")]      string       JobId,
        [property: JsonPropertyName("model_type")]  string       ModelType,
        [property: JsonPropertyName("state")]       string       State,
        [property: JsonPropertyName("model_id")]    string       ModelId,
        [property: JsonPropertyName("onnx_path")]   string       OnnxPath,
        [property: JsonPropertyName("joblib_path")] string       JoblibPath,
        [property: JsonPropertyName("ipfs_cid")]    string       IpfsCid,
        [property: JsonPropertyName("metrics")]     JsonElement? Metrics,
        [property: JsonPropertyName("exported_at")] DateTimeOffset ExportedAt);
}
