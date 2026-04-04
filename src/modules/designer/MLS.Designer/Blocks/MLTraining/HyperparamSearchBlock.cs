using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Constants;
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

    // Track dispatched hyperparams so they can be paired with the trial result
    private readonly Dictionary<Guid, JsonElement> _pendingHyperparams = new();

    // Results: (hyperparams, metric)
    private readonly List<(JsonElement Hyperparams, float Metric)> _trialResults = [];

    private readonly BlockParameter<string> _searchStrategyParam =
        new("SearchStrategy",   "Search Strategy",    "Grid, Random, or Bayesian",                              "Random");
    private readonly BlockParameter<int>    _maxTrialsParam =
        new("MaxTrials",        "Max Trials",          "Maximum number of trials",                               20, MinValue: 1,  MaxValue: 200);
    private readonly BlockParameter<int>    _epochsPerTrialParam =
        new("EpochsPerTrial",   "Epochs Per Trial",    "Epochs per trial run (neural network models only)",      50, MinValue: 1,  MaxValue: 1000);
    private readonly BlockParameter<string> _modelTypeParam =
        new("ModelType",        "Model Type",          "model-t, model-a, or model-d",                          "model-t");
    private readonly BlockParameter<string> _optimizeMetricParam =
        new("OptimizeMetric",   "Optimize Metric",     "Metric to optimise (val_loss, accuracy, f1_macro)",      "val_loss");
    private readonly BlockParameter<bool>   _minimizeParam =
        new("Minimize",         "Minimize",            "True = minimise the metric; False = maximise",           true);

    // ── Algorithm + extended hyperparameter parameters ────────────────────────

    private readonly BlockParameter<string> _algorithmTypeParam =
        new("AlgorithmType",    "Algorithm Type",
            "NeuralNetwork, LogisticRegression, GradientBoosting, or RandomForest",                              "NeuralNetwork");
    private readonly BlockParameter<int>    _nEstimatorsParam =
        new("NEstimators",      "N Estimators",
            "Number of trees / estimators (GradientBoosting, RandomForest)",                                     100, MinValue: 10,  MaxValue: 5000);
    private readonly BlockParameter<int>    _maxDepthParam =
        new("MaxDepth",         "Max Depth",
            "Maximum tree depth (GradientBoosting, RandomForest; 0 = unlimited)",                                6, MinValue: 0,   MaxValue: 64);
    private readonly BlockParameter<float>  _subsampleParam =
        new("Subsample",        "Subsample Ratio",
            "Fraction of samples per tree (GradientBoosting)",                                                   0.8f, MinValue: 0.1f, MaxValue: 1.0f, IsOptimizable: true);
    private readonly BlockParameter<float>  _dropoutRangeMaxParam =
        new("DropoutRangeMax",  "Dropout Range Max",
            "Upper bound for dropout search range (NeuralNetwork)",                                              0.4f, MinValue: 0f,   MaxValue: 0.8f, IsOptimizable: true);
    private readonly BlockParameter<float>  _weightDecayParam =
        new("WeightDecay",      "Weight Decay",
            "L2 regularisation / weight decay (NeuralNetwork)",                                                  1e-4f, MinValue: 0f, MaxValue: 0.1f, IsOptimizable: true);
    private readonly BlockParameter<int>    _cpuThreadsParam =
        new("CpuThreads",       "CPU Threads",
            "Number of CPU threads for training (0 = auto-detect)",                                              0, MinValue: 0,   MaxValue: 256);

    /// <inheritdoc/>
    public override string BlockType   => "HyperparamSearchBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Hyperparam Search";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_searchStrategyParam, _maxTrialsParam, _epochsPerTrialParam,
         _modelTypeParam, _optimizeMetricParam, _minimizeParam,
         _algorithmTypeParam, _nEstimatorsParam, _maxDepthParam,
         _subsampleParam, _dropoutRangeMaxParam, _weightDecayParam, _cpuThreadsParam];

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
        _pendingHyperparams.Clear();
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
            State:       TrainingState.SearchStarted,
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

        // Retrieve hyperparams that were dispatched for this trial
        _pendingHyperparams.Remove(complete.JobId, out var hp);
        var trialHp = hp.ValueKind == JsonValueKind.Undefined
            ? JsonSerializer.SerializeToElement(new { })
            : hp;

        float metric = ExtractMetric(complete.Metrics, _optimizeMetricParam.DefaultValue);

        _trialResults.Add((trialHp, metric));

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
            State:           TrainingState.SearchRunning,
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

        // Record hyperparams so they can be paired with the trial result on completion
        _pendingHyperparams[jobId] = hyperparams;

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
            State:           TrainingState.SearchComplete,
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
        var strategy      = _searchStrategyParam.DefaultValue;
        var algorithmType = _algorithmTypeParam.DefaultValue;

        // For tree-based / linear algorithms (LogisticRegression, GradientBoosting, RandomForest),
        // the relevant hyperparameters differ from neural-network params.
        if (!algorithmType.Equals("NeuralNetwork", StringComparison.OrdinalIgnoreCase))
            return GenerateTreeHyperparams(trialIdx, algorithmType);

        return strategy.Equals("Grid", StringComparison.OrdinalIgnoreCase)
            ? GenerateGridHyperparams(trialIdx)
            : strategy.Equals("Bayesian", StringComparison.OrdinalIgnoreCase) && _trialResults.Count >= 5
                ? GenerateBayesianHyperparams()
                : GenerateRandomHyperparams();
    }

    /// <summary>
    /// Generates hyperparameters for tree/linear algorithms
    /// (LogisticRegression, GradientBoosting, RandomForest).
    /// Parameters are embedded in the dispatched payload so that the Python pipeline
    /// can route to the appropriate scikit-learn estimator.
    /// </summary>
    private JsonElement GenerateTreeHyperparams(int trialIdx, string algorithmType)
    {
        var rng          = Random.Shared;
        int nEst         = _nEstimatorsParam.DefaultValue;
        int maxDepth     = _maxDepthParam.DefaultValue;
        float subsample  = _subsampleParam.DefaultValue;
        int cpuThreads   = _cpuThreadsParam.DefaultValue;

        // Perturb around configured defaults for non-grid strategies
        var strategy = _searchStrategyParam.DefaultValue;
        if (!strategy.Equals("Grid", StringComparison.OrdinalIgnoreCase))
        {
            // Random or Bayesian: vary key parameters within sensible bounds
            nEst     = Math.Clamp(nEst + rng.Next(-nEst / 4, nEst / 4 + 1), 10, 5000);
            maxDepth = Math.Clamp(maxDepth + rng.Next(-2, 3), 1, 64);
            subsample = Math.Clamp(subsample + (float)(rng.NextDouble() - 0.5) * 0.2f, 0.1f, 1.0f);
        }

        // LogisticRegression does not use learning_rate for fitting; use 0 as sentinel
        float lr = algorithmType.Equals("LogisticRegression", StringComparison.OrdinalIgnoreCase)
            ? 0f
            : (float)Math.Pow(10, rng.NextDouble() * 2 - 2);  // log ∈ [0.01, 1.0] for boosting

        return JsonSerializer.SerializeToElement(new
        {
            algorithm_type = algorithmType.ToLowerInvariant(),
            n_estimators   = nEst,
            max_depth      = maxDepth,
            subsample      = subsample,
            learning_rate  = lr,
            n_jobs         = cpuThreads == 0 ? -1 : cpuThreads,  // -1 = use all available cores
        });
    }

    private JsonElement GenerateGridHyperparams(int idx)
    {
        // Grid over learning_rate × batch_size (neural network)
        float[] lrGrid        = [1e-4f, 5e-4f, 1e-3f, 5e-3f];
        int[]   batchGrid     = [256, 512];
        int     combinations  = lrGrid.Length * batchGrid.Length;
        int     pos           = idx % combinations;
        float   lr            = lrGrid[pos % lrGrid.Length];
        int     batch         = batchGrid[pos / lrGrid.Length];

        return JsonSerializer.SerializeToElement(new
        {
            algorithm_type = "neural_network",
            learning_rate  = lr,
            batch_size     = batch,
            dropout_rate   = 0.2f,
            weight_decay   = _weightDecayParam.DefaultValue,
            n_jobs         = _cpuThreadsParam.DefaultValue == 0 ? -1 : _cpuThreadsParam.DefaultValue,
        });
    }

    private JsonElement GenerateRandomHyperparams()
    {
        var rng          = Random.Shared;
        double lrExp     = rng.NextDouble() * 3 - 5;   // log10 ∈ [-5, -2]
        float  lr        = (float)Math.Pow(10, lrExp);
        int[]  batches   = [128, 256, 512, 1024];
        float  dropout   = (float)(rng.NextDouble() * _dropoutRangeMaxParam.DefaultValue);

        return JsonSerializer.SerializeToElement(new
        {
            algorithm_type = "neural_network",
            learning_rate  = lr,
            batch_size     = batches[rng.Next(batches.Length)],
            dropout_rate   = dropout,
            weight_decay   = _weightDecayParam.DefaultValue,
            n_jobs         = _cpuThreadsParam.DefaultValue == 0 ? -1 : _cpuThreadsParam.DefaultValue,
        });
    }

    private JsonElement GenerateBayesianHyperparams()
    {
        // TPE approximation: sample around the best known configuration with Gaussian noise
        var (bestHp, _) = GetBestTrial();

        float baseLr      = 1e-3f;
        int   baseBatch   = 512;
        float baseDropout = 0.2f;

        if (bestHp.ValueKind == JsonValueKind.Object)
        {
            if (bestHp.TryGetProperty("learning_rate", out var lr)) lr.TryGetSingle(out baseLr);
            if (bestHp.TryGetProperty("batch_size",    out var bs)) bs.TryGetInt32(out baseBatch);
            if (bestHp.TryGetProperty("dropout_rate",  out var dr)) dr.TryGetSingle(out baseDropout);
        }

        var rng   = Random.Shared;
        // Perturb log(lr) by ±0.3 standard deviations
        double newLrLog   = Math.Log10(baseLr) + (rng.NextDouble() - 0.5) * 0.6;
        float  newLr      = Math.Clamp((float)Math.Pow(10, newLrLog), 1e-6f, 0.1f);
        float  newDropout = Math.Clamp(baseDropout + (float)(rng.NextDouble() - 0.5) * 0.1f, 0f, _dropoutRangeMaxParam.DefaultValue);

        return JsonSerializer.SerializeToElement(new
        {
            algorithm_type = "neural_network",
            learning_rate  = newLr,
            batch_size     = baseBatch,
            dropout_rate   = newDropout,
            weight_decay   = _weightDecayParam.DefaultValue,
            n_jobs         = _cpuThreadsParam.DefaultValue == 0 ? -1 : _cpuThreadsParam.DefaultValue,
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private bool IsEarlyStop()
    {
        // Stop if the last 5 trials show no improvement compared with the best result
        // achieved before that recent window.
        const int earlyStopWindowSize = 5;
        if (_trialResults.Count <= earlyStopWindowSize) return false;

        bool minimize = _minimizeParam.DefaultValue;

        var priorTrials  = _trialResults.Take(_trialResults.Count - earlyStopWindowSize).ToArray();
        var recentTrials = _trialResults.TakeLast(earlyStopWindowSize).ToArray();

        float bestBeforeRecent = minimize
            ? priorTrials.Min(r => r.Metric)
            : priorTrials.Max(r => r.Metric);

        float bestRecent = minimize
            ? recentTrials.Min(r => r.Metric)
            : recentTrials.Max(r => r.Metric);

        return minimize ? bestRecent >= bestBeforeRecent : bestRecent <= bestBeforeRecent;
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
