using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Contracts.Designer;
using MLS.Core.Designer;
using MLS.Designer.Blocks;
using MLS.Designer.Services;

namespace MLS.Designer.Blocks.MLTraining;

/// <summary>
/// Hyperparameter search block — orchestrates a grid, random, or Bayesian search loop
/// over the model hyperparameter space using an Optuna-compatible trial management strategy.
/// </summary>
/// <remarks>
/// <para>
/// This block accepts a prepared <see cref="BlockSocketType.FeatureVector"/> dataset (from
/// <c>TrainSplitBlock</c>), dispatches successive <c>TRAINING_JOB_START</c> envelopes via
/// <see cref="ITrainingDispatcher"/>, and collects results from the Shell VM.
/// When all trials are complete (or an early-stop criterion is met), it emits a
/// <see cref="BlockSocketType.TrainingStatus"/> signal with the best hyperparameter
/// configuration and its associated metrics.
/// </para>
/// <para>
/// Search strategies:
/// <list type="bullet">
///   <item><c>Grid</c> — exhaustive enumeration of a Cartesian parameter grid.</item>
///   <item><c>Random</c> — uniform random sampling from parameter ranges.</item>
///   <item><c>Bayesian</c> — Tree-structured Parzen Estimator (TPE) approximation
///         using running trial history to focus on promising regions.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class HyperparamSearchBlock : BlockBase
{
    private readonly ITrainingDispatcher _dispatcher;

    // ── Trial state ───────────────────────────────────────────────────────────────
    private JsonElement? _datasetSignal;   // Stored split bundle from TrainSplitBlock
    private string       _modelType   = "model-t";
    private int          _trialIndex  = 0;
    private Guid?        _activeTrial = null;

    // Results: (jobId → (hyperparams, metric))
    private readonly List<(JsonElement Hyperparams, float Metric)> _trialResults = [];

    private readonly BlockParameter<string> _searchStrategyParam =
        new("SearchStrategy",  "Search Strategy",   "Grid, Random, or Bayesian",   "Random");
    private readonly BlockParameter<int>    _maxTrialsParam =
        new("MaxTrials",       "Max Trials",         "Maximum number of trials",    20, MinValue: 1, MaxValue: 200);
    private readonly BlockParameter<int>    _epochsPerTrialParam =
        new("EpochsPerTrial",  "Epochs Per Trial",   "Epochs per trial run",        50, MinValue: 1, MaxValue: 1000);
    private readonly BlockParameter<string> _modelTypeParam =
        new("ModelType",       "Model Type",         "model-t, model-a, or model-d", "model-t");
    private readonly BlockParameter<string> _optimizeMetricParam =
        new("OptimizeMetric",  "Optimize Metric",    "Metric to optimise (val_loss, accuracy, f1_macro)", "val_loss");
    private readonly BlockParameter<bool>   _minimizeParam =
        new("Minimize",        "Minimize",           "True = minimise the metric; False = maximise", true);

    /// <inheritdoc/>
    public override string BlockType   => "HyperparamSearchBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Hyperparam Search";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_searchStrategyParam, _maxTrialsParam, _epochsPerTrialParam,
         _modelTypeParam, _optimizeMetricParam, _minimizeParam];

    /// <summary>Initialises a <see cref="HyperparamSearchBlock"/> with the injected dispatcher.</summary>
    public HyperparamSearchBlock(ITrainingDispatcher dispatcher) : base(
        [BlockSocket.Input("dataset_input",   BlockSocketType.FeatureVector)],
        [BlockSocket.Output("search_output",  BlockSocketType.TrainingStatus)])
    {
        _dispatcher = dispatcher;
        _dispatcher.JobCompleted += OnTrialCompletedAsync;
    }

    /// <summary>Default constructor used by <see cref="Services.BlockRegistry"/> (requires factory).</summary>
    public HyperparamSearchBlock() : this(new NullTrainingDispatcher()) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _datasetSignal = null;
        _modelType     = "model-t";
        _trialIndex    = 0;
        _activeTrial   = null;
        _trialResults.Clear();
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        _dispatcher.JobCompleted -= OnTrialCompletedAsync;
        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.FeatureVector)
            return null;

        if (!ExtractModelType(signal.Value, out var modelType))
            return null;

        // Store the dataset for subsequent trial dispatches
        _datasetSignal = signal.Value;
        _modelType     = string.IsNullOrWhiteSpace(modelType) ? _modelTypeParam.DefaultValue : modelType;
        _trialIndex    = 0;
        _trialResults.Clear();

        // Emit SEARCH_STARTED status and dispatch the first trial
        await DispatchNextTrialAsync(ct).ConfigureAwait(false);

        var started = new SearchStatus(
            ModelType:   _modelType,
            State:       "SEARCH_STARTED",
            TrialIndex:  0,
            MaxTrials:   _maxTrialsParam.DefaultValue,
            BestMetric:  float.NaN,
            BestHyperparams: null);

        return EmitObject(BlockId, "search_output", BlockSocketType.TrainingStatus, started);
    }

    // ── Trial lifecycle ───────────────────────────────────────────────────────────

    private async ValueTask OnTrialCompletedAsync(TrainingJobCompletePayload complete, CancellationToken ct)
    {
        // Ignore completions from other jobs
        if (_activeTrial is null || _activeTrial.Value != complete.JobId) return;

        _activeTrial = null;

        float metric = ExtractMetric(complete.Metrics, _optimizeMetricParam.DefaultValue);
        var   hp     = complete.Metrics; // reuse; the hyperparams are embedded by the Shell VM

        _trialResults.Add((hp, metric));

        _trialIndex++;

        if (_trialIndex >= _maxTrialsParam.DefaultValue || IsEarlyStop())
        {
            await EmitBestResultAsync(ct).ConfigureAwait(false);
            return;
        }

        // Dispatch next trial
        await DispatchNextTrialAsync(ct).ConfigureAwait(false);

        // Emit progress
        var (bestHp, bestMetric) = GetBestTrial();
        var progress = new SearchStatus(
            ModelType:       _modelType,
            State:           "SEARCH_RUNNING",
            TrialIndex:      _trialIndex,
            MaxTrials:       _maxTrialsParam.DefaultValue,
            BestMetric:      bestMetric,
            BestHyperparams: bestHp);

        await EmitSignalAsync(
            EmitObject(BlockId, "search_output", BlockSocketType.TrainingStatus, progress), ct)
            .ConfigureAwait(false);
    }

    private async Task DispatchNextTrialAsync(CancellationToken ct)
    {
        if (_datasetSignal is null) return;

        var hyperparams = GenerateHyperparams(_trialIndex);
        var jobId       = Guid.NewGuid();
        _activeTrial    = jobId;

        var payload = new TrainingJobStartPayload(
            JobId:                jobId,
            ModelType:            _modelType,
            FeatureSchemaVersion: 1,
            Hyperparams:          hyperparams,
            DataRange:            new TrainingDataRange(
                                      From: DateTimeOffset.UtcNow.AddYears(-1),
                                      To:   DateTimeOffset.UtcNow));

        await _dispatcher.DispatchJobAsync(payload, ct).ConfigureAwait(false);
    }

    private async ValueTask EmitBestResultAsync(CancellationToken ct)
    {
        var (bestHp, bestMetric) = GetBestTrial();

        var best = new SearchStatus(
            ModelType:       _modelType,
            State:           "SEARCH_COMPLETE",
            TrialIndex:      _trialIndex,
            MaxTrials:       _maxTrialsParam.DefaultValue,
            BestMetric:      bestMetric,
            BestHyperparams: bestHp);

        await EmitSignalAsync(
            EmitObject(BlockId, "search_output", BlockSocketType.TrainingStatus, best), ct)
            .ConfigureAwait(false);
    }

    // ── Search strategies ─────────────────────────────────────────────────────────

    private JsonElement GenerateHyperparams(int trialIdx)
    {
        var strategy = _searchStrategyParam.DefaultValue;

        return strategy.Equals("Grid", StringComparison.OrdinalIgnoreCase)
            ? GenerateGridHyperparams(trialIdx)
            : strategy.Equals("Bayesian", StringComparison.OrdinalIgnoreCase) && _trialResults.Count >= 5
                ? GenerateBayesianHyperparams()
                : GenerateRandomHyperparams();
    }

    private static JsonElement GenerateGridHyperparams(int idx)
    {
        // Grid over learning_rate × batch_size
        float[] lrGrid        = [1e-4f, 5e-4f, 1e-3f, 5e-3f];
        int[]   batchGrid     = [256, 512];
        int     combinations  = lrGrid.Length * batchGrid.Length;
        int     pos           = idx % combinations;
        float   lr            = lrGrid[pos % lrGrid.Length];
        int     batch         = batchGrid[pos / lrGrid.Length];

        return JsonSerializer.SerializeToElement(new
        {
            learning_rate = lr,
            batch_size    = batch,
            dropout_rate  = 0.2f,
        });
    }

    private static JsonElement GenerateRandomHyperparams()
    {
        var rng          = Random.Shared;
        double lrExp     = rng.NextDouble() * 3 - 5;   // log10 ∈ [-5, -2]
        float  lr        = (float)Math.Pow(10, lrExp);
        int[]  batches   = [128, 256, 512, 1024];
        float  dropout   = (float)(rng.NextDouble() * 0.4);  // [0, 0.4]

        return JsonSerializer.SerializeToElement(new
        {
            learning_rate = lr,
            batch_size    = batches[rng.Next(batches.Length)],
            dropout_rate  = dropout,
        });
    }

    private JsonElement GenerateBayesianHyperparams()
    {
        // TPE approximation: sample around the best known configuration with Gaussian noise
        var (bestHp, _) = GetBestTrial();

        float baseLr    = 1e-3f;
        int   baseBatch = 512;
        float baseDropout = 0.2f;

        if (bestHp.ValueKind == JsonValueKind.Object)
        {
            if (bestHp.TryGetProperty("learning_rate", out var lr)) lr.TryGetSingle(out baseLr);
            if (bestHp.TryGetProperty("batch_size",    out var bs)) bs.TryGetInt32(out baseBatch);
            if (bestHp.TryGetProperty("dropout_rate",  out var dr)) dr.TryGetSingle(out baseDropout);
        }

        var rng   = Random.Shared;
        // Perturb log(lr) by ±0.3 standard deviations
        double newLrLog  = Math.Log10(baseLr) + (rng.NextDouble() - 0.5) * 0.6;
        float  newLr     = Math.Clamp((float)Math.Pow(10, newLrLog), 1e-6f, 0.1f);
        float  newDropout = Math.Clamp(baseDropout + (float)(rng.NextDouble() - 0.5) * 0.1f, 0f, 0.5f);

        return JsonSerializer.SerializeToElement(new
        {
            learning_rate = newLr,
            batch_size    = baseBatch,
            dropout_rate  = newDropout,
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private bool IsEarlyStop()
    {
        // Stop if the last 5 trials show no improvement
        if (_trialResults.Count < 5) return false;
        bool minimize   = _minimizeParam.DefaultValue;
        var  recent     = _trialResults.TakeLast(5).Select(r => r.Metric).ToArray();
        float bestSoFar = minimize ? _trialResults.Select(r => r.Metric).Min() : _trialResults.Select(r => r.Metric).Max();
        float bestRecent = minimize ? recent.Min() : recent.Max();
        return minimize ? bestRecent >= bestSoFar : bestRecent <= bestSoFar;
    }

    private (JsonElement Hyperparams, float Metric) GetBestTrial()
    {
        if (_trialResults.Count == 0)
            return (JsonSerializer.SerializeToElement(new { }), float.NaN);

        bool minimize = _minimizeParam.DefaultValue;
        return minimize
            ? _trialResults.MinBy(r => r.Metric)
            : _trialResults.MaxBy(r => r.Metric);
    }

    private static float ExtractMetric(JsonElement metrics, string metricName)
    {
        if (metrics.ValueKind != JsonValueKind.Object) return float.NaN;
        if (metrics.TryGetProperty(metricName, out var el) && el.TryGetSingle(out var v)) return v;
        // fallback: use val_loss
        if (metrics.TryGetProperty("val_loss",  out var vl) && vl.TryGetSingle(out var vlv)) return vlv;
        return float.NaN;
    }

    private static bool ExtractModelType(JsonElement value, out string modelType)
    {
        modelType = string.Empty;
        if (value.ValueKind != JsonValueKind.Object) return false;
        if (value.TryGetProperty("model_type", out var mt)) modelType = mt.GetString() ?? string.Empty;
        return true;
    }

    // ── Wire types ────────────────────────────────────────────────────────────────

    internal sealed record SearchStatus(
        [property: JsonPropertyName("model_type")]        string       ModelType,
        [property: JsonPropertyName("state")]             string       State,
        [property: JsonPropertyName("trial_index")]       int          TrialIndex,
        [property: JsonPropertyName("max_trials")]        int          MaxTrials,
        [property: JsonPropertyName("best_metric")]       float        BestMetric,
        [property: JsonPropertyName("best_hyperparams")]  JsonElement? BestHyperparams);

    // ── Null dispatcher (for registry introspection) ──────────────────────────────

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
