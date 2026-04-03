using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Contracts.Designer;
using MLS.Core.Designer;
using MLS.Designer.Blocks;
using MLS.Designer.Services;

namespace MLS.Designer.Blocks.MLTraining;

/// <summary>
/// Train model block — the central dispatch node of the ML training pipeline.
/// Receives a split <see cref="BlockSocketType.FeatureVector"/> bundle from
/// <c>TrainSplitBlock</c>, builds a <c>TRAINING_JOB_START</c> envelope, and
/// dispatches it to Shell VM via <see cref="ITrainingDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule</b>: this block MUST emit the envelope and MUST NOT call Python directly.
/// The Shell VM spawns <c>python training_pipeline.py --config &lt;path&gt;</c> in response.
/// </para>
/// <para>
/// Progress and completion signals are streamed back asynchronously via
/// <see cref="ITrainingDispatcher.ProgressReceived"/> and
/// <see cref="ITrainingDispatcher.JobCompleted"/>, triggering downstream
/// <see cref="BlockSocketType.TrainingStatus"/> signals on the canvas.
/// </para>
/// </remarks>
public sealed class TrainModelBlock : BlockBase
{
    private readonly ITrainingDispatcher _dispatcher;

    private readonly BlockParameter<string> _modelTypeParam =
        new("ModelType",           "Model Type",              "model-t, model-a, or model-d",      "model-t");
    private readonly BlockParameter<int>    _epochsParam =
        new("Epochs",              "Epochs",                  "Maximum training epochs",            100, MinValue: 1,    MaxValue: 2000);
    private readonly BlockParameter<int>    _batchSizeParam =
        new("BatchSize",           "Batch Size",              "Mini-batch size",                    512, MinValue: 16,   MaxValue: 4096);
    private readonly BlockParameter<float>  _learningRateParam =
        new("LearningRate",        "Learning Rate",           "Initial learning rate",              1e-3f, MinValue: 1e-6f, MaxValue: 0.1f, IsOptimizable: true);
    private readonly BlockParameter<int>    _featureSchemaParam =
        new("FeatureSchemaVersion","Feature Schema Version",  "Must match model expected input dim", 1, MinValue: 1, MaxValue: 100);

    /// <inheritdoc/>
    public override string BlockType   => "TrainModelBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Train Model";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_modelTypeParam, _epochsParam, _batchSizeParam, _learningRateParam, _featureSchemaParam];

    /// <summary>Initialises a <see cref="TrainModelBlock"/> with the injected dispatcher.</summary>
    public TrainModelBlock(ITrainingDispatcher dispatcher) : base(
        [BlockSocket.Input("split_input", BlockSocketType.FeatureVector)],
        [BlockSocket.Output("training_status", BlockSocketType.TrainingStatus)])
    {
        _dispatcher = dispatcher;
        _dispatcher.ProgressReceived += OnProgressReceivedAsync;
        _dispatcher.JobCompleted     += OnJobCompletedAsync;
    }

    /// <summary>Default constructor used by <see cref="Services.BlockRegistry"/> (requires factory).</summary>
    public TrainModelBlock() : this(new NullTrainingDispatcher()) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        _dispatcher.ProgressReceived -= OnProgressReceivedAsync;
        _dispatcher.JobCompleted     -= OnJobCompletedAsync;
        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.FeatureVector)
            return null;

        if (!TryExtractSplitBundle(signal.Value,
                out var modelType, out var featureNames, out var hyperparamOverride,
                out var dataFrom, out var dataTo))
            return null;

        // Allow signal-embedded model_type to override the block parameter
        var effectiveModel = string.IsNullOrWhiteSpace(modelType)
            ? _modelTypeParam.DefaultValue
            : modelType;

        var jobId = Guid.NewGuid();

        // Build hyperparams: merge block defaults with any override from upstream (e.g. HyperparamSearchBlock)
        var hyperparams = BuildHyperparams(hyperparamOverride);

        var jobStart = new TrainingJobStartPayload(
            JobId:                jobId,
            ModelType:            effectiveModel,
            FeatureSchemaVersion: _featureSchemaParam.DefaultValue,
            Hyperparams:          hyperparams,
            DataRange:            new TrainingDataRange(
                                      From: dataFrom,
                                      To:   dataTo));

        await _dispatcher.DispatchJobAsync(jobStart, ct).ConfigureAwait(false);

        // Emit PENDING status immediately so downstream blocks see the job
        var pending = new TrainingStatusValue(
            JobId:       jobId.ToString("N"),
            ModelType:   effectiveModel,
            State:       "PENDING",
            Epoch:       0,
            TotalEpochs: _epochsParam.DefaultValue,
            TrainLoss:   0f,
            ValLoss:     0f,
            Accuracy:    0f,
            ElapsedMs:   0L,
            EtaMs:       0L);

        return EmitObject(BlockId, "training_status", BlockSocketType.TrainingStatus, pending);
    }

    // ── Dispatcher event handlers ─────────────────────────────────────────────────

    private ValueTask OnProgressReceivedAsync(TrainingJobProgressPayload progress, CancellationToken ct)
    {
        var status = new TrainingStatusValue(
            JobId:       progress.JobId.ToString("N"),
            ModelType:   _modelTypeParam.DefaultValue,
            State:       "TRAINING",
            Epoch:       progress.Epoch,
            TotalEpochs: progress.TotalEpochs,
            TrainLoss:   progress.TrainLoss,
            ValLoss:     progress.ValLoss,
            Accuracy:    progress.Accuracy,
            ElapsedMs:   progress.ElapsedMs,
            EtaMs:       progress.EtaMs);

        return EmitSignalAsync(
            EmitObject(BlockId, "training_status", BlockSocketType.TrainingStatus, status), ct);
    }

    private ValueTask OnJobCompletedAsync(TrainingJobCompletePayload complete, CancellationToken ct)
    {
        var status = new TrainingStatusValue(
            JobId:       complete.JobId.ToString("N"),
            ModelType:   complete.ModelType,
            State:       "COMPLETE",
            Epoch:       0,
            TotalEpochs: 0,
            TrainLoss:   0f,
            ValLoss:     0f,
            Accuracy:    0f,
            ElapsedMs:   complete.DurationMs,
            EtaMs:       0L,
            ModelId:     complete.ModelId,
            OnnxPath:    complete.OnnxPath,
            JoblibPath:  complete.JoblibPath,
            IpfsCid:     complete.IpfsCid,
            Metrics:     complete.Metrics);

        return EmitSignalAsync(
            EmitObject(BlockId, "training_status", BlockSocketType.TrainingStatus, status), ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private JsonElement BuildHyperparams(JsonElement? @override)
    {
        // If an override was embedded in the signal (e.g. from HyperparamSearchBlock), use it;
        // otherwise construct defaults from block parameters.
        if (@override.HasValue && @override.Value.ValueKind == JsonValueKind.Object)
            return @override.Value;

        var hp = new
        {
            epochs         = _epochsParam.DefaultValue,
            batch_size     = _batchSizeParam.DefaultValue,
            learning_rate  = _learningRateParam.DefaultValue,
        };
        return JsonSerializer.SerializeToElement(hp);
    }

    private static bool TryExtractSplitBundle(
        JsonElement value,
        out string   modelType,
        out string[] featureNames,
        out JsonElement? hyperparamOverride,
        out DateTimeOffset dataFrom,
        out DateTimeOffset dataTo)
    {
        modelType        = string.Empty;
        featureNames     = [];
        hyperparamOverride = null;
        dataFrom         = DateTimeOffset.UtcNow.AddYears(-1);
        dataTo           = DateTimeOffset.UtcNow;

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (value.TryGetProperty("model_type",    out var mt)) modelType = mt.GetString() ?? string.Empty;
        if (value.TryGetProperty("feature_names", out var fn) && fn.ValueKind == JsonValueKind.Array)
            featureNames = fn.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();

        if (value.TryGetProperty("hyperparams", out var hp) && hp.ValueKind == JsonValueKind.Object)
            hyperparamOverride = hp;

        if (value.TryGetProperty("data_from", out var dfEl) && dfEl.TryGetDateTimeOffset(out var df))
            dataFrom = df;
        if (value.TryGetProperty("data_to",   out var dtEl) && dtEl.TryGetDateTimeOffset(out var dt))
            dataTo = dt;

        // Accept any FeatureVector that contains at least a model_type or samples
        return true;
    }

    // ── Wire types ────────────────────────────────────────────────────────────────

    internal sealed record TrainingStatusValue(
        [property: JsonPropertyName("job_id")]       string   JobId,
        [property: JsonPropertyName("model_type")]   string   ModelType,
        [property: JsonPropertyName("state")]        string   State,
        [property: JsonPropertyName("epoch")]        int      Epoch,
        [property: JsonPropertyName("total_epochs")] int      TotalEpochs,
        [property: JsonPropertyName("train_loss")]   float    TrainLoss,
        [property: JsonPropertyName("val_loss")]     float    ValLoss,
        [property: JsonPropertyName("accuracy")]     float    Accuracy,
        [property: JsonPropertyName("elapsed_ms")]   long     ElapsedMs,
        [property: JsonPropertyName("eta_ms")]       long     EtaMs,
        [property: JsonPropertyName("model_id")]     string?  ModelId    = null,
        [property: JsonPropertyName("onnx_path")]    string?  OnnxPath   = null,
        [property: JsonPropertyName("joblib_path")]  string?  JoblibPath = null,
        [property: JsonPropertyName("ipfs_cid")]     string?  IpfsCid    = null,
        [property: JsonPropertyName("metrics")]      JsonElement? Metrics = null);

    /// <summary>
    /// Stub dispatcher used when the block is instantiated without DI
    /// (e.g. during registry metadata introspection).
    /// </summary>
    private sealed class NullTrainingDispatcher : ITrainingDispatcher
    {
#pragma warning disable CS0067 // Interface-required events — never raised by this stub
        public event Func<TrainingJobProgressPayload, CancellationToken, ValueTask>? ProgressReceived;
        public event Func<TrainingJobCompletePayload,  CancellationToken, ValueTask>? JobCompleted;
#pragma warning restore CS0067
        public ValueTask<Guid> DispatchJobAsync(TrainingJobStartPayload payload, CancellationToken ct)
            => ValueTask.FromResult(payload.JobId);
    }
}
