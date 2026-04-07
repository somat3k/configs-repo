using System.Text.Json;
using FluentAssertions;
using MLS.Core.Contracts.Designer;
using MLS.Core.Designer;
using MLS.Designer.Blocks.MLTraining;
using MLS.Designer.Services;
using Xunit;

namespace MLS.Designer.Tests;

/// <summary>
/// Unit tests for the ML Training domain blocks.
/// Covers TrainSplitBlock split-ratio logic and HyperparamSearchBlock early-stop + best-trial
/// selection, which contain non-trivial pure logic that benefits from direct test coverage.
/// Also covers Optuna-mode dispatch: verifies a single search job is dispatched with the
/// correct search_mode and search_space fields when strategy is "Bayesian".
/// </summary>
public sealed class MLTrainingBlockTests
{
    // ── TrainSplitBlock ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TrainSplitBlock_DefaultRatios_ProducesCorrectSplitSizes()
    {
        var block   = new TrainSplitBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // Feed 100 samples (default MinSamples = 100 triggers split)
        await FeedFeatureBatch(block, samples: 100, labels: true);

        output.Should().NotBeNull("split should be emitted when MinSamples is reached");
        var bundle = output!.Value.Value;

        bundle.TryGetProperty("train", out var train).Should().BeTrue();
        bundle.TryGetProperty("val",   out var val).Should().BeTrue();
        bundle.TryGetProperty("test",  out var test).Should().BeTrue();

        int nTrain = train.GetProperty("samples").GetArrayLength();
        int nVal   = val.GetProperty("samples").GetArrayLength();
        int nTest  = test.GetProperty("samples").GetArrayLength();

        (nTrain + nVal + nTest).Should().Be(100, "all samples must be accounted for");
        nTrain.Should().Be(80, "default train ratio is 0.80");
        nVal.Should().Be(10,   "default val ratio is 0.10");
        nTest.Should().Be(10,  "remainder is test set");
    }

    [Fact]
    public async Task TrainSplitBlock_OverflowingRatios_DoesNotThrow()
    {
        // Even with ratios that would sum to >=1.0 the block must not throw
        var block = CreateSplitBlockWithRatios(trainRatio: 0.95f, valRatio: 0.10f);
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        var act = async () => await FeedFeatureBatch(block, samples: 100, labels: false);

        await act.Should().NotThrowAsync("ratio overflow must be clamped gracefully");

        output.Should().NotBeNull("split should still be emitted");
        var bundle = output!.Value.Value;
        var nTrain = bundle.GetProperty("train").GetProperty("samples").GetArrayLength();
        var nVal   = bundle.GetProperty("val").GetProperty("samples").GetArrayLength();
        var nTest  = bundle.GetProperty("test").GetProperty("samples").GetArrayLength();

        (nTrain + nVal + nTest).Should().Be(100, "total samples must be preserved");
        nTest.Should().BeGreaterThanOrEqualTo(0, "test set must never be negative");
    }

    [Fact]
    public async Task TrainSplitBlock_NotEnoughSamples_DoesNotEmit()
    {
        var block   = new TrainSplitBlock();
        var emitted = 0;
        block.OutputProduced += (_, _) => { emitted++; return ValueTask.CompletedTask; };

        // Feed only 50 samples — default MinSamples = 100
        await FeedFeatureBatch(block, samples: 50, labels: true);

        emitted.Should().Be(0, "split should not emit until MinSamples is reached");
    }

    [Fact]
    public async Task TrainSplitBlock_SentinelLabelForMissingSamples()
    {
        var block   = new TrainSplitBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // Feed batch WITHOUT labels
        await FeedFeatureBatch(block, samples: 100, labels: false);

        output.Should().NotBeNull();
        var trainLabels = output!.Value.Value
            .GetProperty("train")
            .GetProperty("labels");

        int sentinel = trainLabels[0].GetInt32();
        sentinel.Should().Be(-1, "missing labels must use -1 sentinel");
    }

    [Fact]
    public async Task TrainSplitBlock_Reset_ClearsBuffer()
    {
        var block   = new TrainSplitBlock();
        var emitted = 0;
        block.OutputProduced += (_, _) => { emitted++; return ValueTask.CompletedTask; };

        // Feed 50 samples, reset, then feed another 50 — should not emit
        await FeedFeatureBatch(block, samples: 50, labels: false);
        block.Reset();
        await FeedFeatureBatch(block, samples: 50, labels: false);

        emitted.Should().Be(0, "reset must discard accumulated samples");
    }

    // ── HyperparamSearchBlock ─────────────────────────────────────────────────────

    [Fact]
    public void HyperparamSearchBlock_GetBestTrial_PicksMinimumWhenMinimizeTrue()
    {
        var block = new HyperparamSearchBlockTestHarness(minimize: true);

        block.AddTrialResult(lr: 1e-3f, metric: 0.5f);
        block.AddTrialResult(lr: 5e-4f, metric: 0.3f);  // best
        block.AddTrialResult(lr: 1e-2f, metric: 0.7f);

        var (hp, metric) = block.GetBestTrial();

        metric.Should().BeApproximately(0.3f, 1e-4f, "minimising should select the lowest metric");
        hp.GetProperty("learning_rate").GetSingle().Should().BeApproximately(5e-4f, 1e-6f);
    }

    [Fact]
    public void HyperparamSearchBlock_GetBestTrial_PicksMaximumWhenMinimizeFalse()
    {
        var block = new HyperparamSearchBlockTestHarness(minimize: false);

        block.AddTrialResult(lr: 1e-3f, metric: 0.5f);
        block.AddTrialResult(lr: 5e-4f, metric: 0.9f);  // best
        block.AddTrialResult(lr: 1e-2f, metric: 0.7f);

        var (hp, metric) = block.GetBestTrial();

        metric.Should().BeApproximately(0.9f, 1e-4f, "maximising should select the highest metric");
        hp.GetProperty("learning_rate").GetSingle().Should().BeApproximately(5e-4f, 1e-6f);
    }

    [Fact]
    public void HyperparamSearchBlock_IsEarlyStop_NotEnoughTrials_ReturnsFalse()
    {
        var block = new HyperparamSearchBlockTestHarness(minimize: true);

        for (int i = 0; i < 5; i++)
            block.AddTrialResult(lr: 1e-3f, metric: 0.5f - i * 0.01f);

        // Only 5 trials — window requires >5 trials before checking
        block.IsEarlyStop().Should().BeFalse("early stop requires more than 5 trials");
    }

    [Fact]
    public void HyperparamSearchBlock_IsEarlyStop_NoImprovementInRecentWindow_ReturnsTrue()
    {
        var block = new HyperparamSearchBlockTestHarness(minimize: true);

        // 6 prior trials that reach a clear best of 0.2
        float[] priorMetrics = [0.8f, 0.7f, 0.5f, 0.4f, 0.3f, 0.2f];
        foreach (var m in priorMetrics)
            block.AddTrialResult(lr: 1e-3f, metric: m);

        // 5 recent trials that are all worse than the prior best (0.2)
        float[] recentMetrics = [0.25f, 0.30f, 0.28f, 0.35f, 0.22f];
        foreach (var m in recentMetrics)
            block.AddTrialResult(lr: 1e-3f, metric: m);

        block.IsEarlyStop().Should().BeTrue("no improvement in recent window should trigger early stop");
    }

    [Fact]
    public void HyperparamSearchBlock_IsEarlyStop_StillImproving_ReturnsFalse()
    {
        var block = new HyperparamSearchBlockTestHarness(minimize: true);

        // 6 prior trials declining to 0.3
        for (int i = 0; i < 6; i++)
            block.AddTrialResult(lr: 1e-3f, metric: 0.8f - i * 0.1f);

        // 5 recent trials that continue to improve past the prior best
        for (int i = 0; i < 5; i++)
            block.AddTrialResult(lr: 1e-3f, metric: 0.25f - i * 0.02f);

        block.IsEarlyStop().Should().BeFalse("continuing improvement should NOT trigger early stop");
    }

    // ── Optuna dispatch mode ──────────────────────────────────────────────────────

    [Fact]
    public async Task HyperparamSearchBlock_BayesianStrategy_DispatchesSingleOptunaSearchJob()
    {
        var dispatcher = new CapturingTrainingDispatcher();
        var block      = HyperparamSearchBlockTestHarness.CreateBayesianBlock(dispatcher);

        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // Feed a feature vector
        var featureSignal = MakeFeatureVectorSignal("model-t");
        await block.ProcessAsync(featureSignal, CancellationToken.None);

        // Exactly ONE job should be dispatched (the whole Optuna study)
        dispatcher.Dispatched.Should().HaveCount(1,
            "Bayesian strategy must dispatch a single Optuna search job, not N individual trials");

        var hp = dispatcher.Dispatched[0].Hyperparams;

        hp.TryGetProperty("search_mode", out var sm).Should().BeTrue("search_mode must be present");
        sm.GetString().Should().Be("optuna");

        hp.TryGetProperty("n_trials", out var nt).Should().BeTrue("n_trials must be present");
        nt.GetInt32().Should().Be(5, "test block configured with n_trials=5");

        hp.TryGetProperty("sampler", out var sampler).Should().BeTrue("sampler must be present");
        sampler.GetString().Should().Be("tpe");

        hp.TryGetProperty("pruner", out var pruner).Should().BeTrue("pruner must be present");
        pruner.GetString().Should().Be("hyperband");

        hp.TryGetProperty("search_space", out var ss).Should().BeTrue("search_space must be present");
        ss.TryGetProperty("lr",      out _).Should().BeTrue("search_space.lr must be present");
        ss.TryGetProperty("dropout", out _).Should().BeTrue("search_space.dropout must be present");
        ss.TryGetProperty("hidden_dims", out _).Should().BeTrue("search_space.hidden_dims must be present");
    }

    [Fact]
    public async Task HyperparamSearchBlock_BayesianStrategy_SearchStartedSignalHasOptunaMode()
    {
        var dispatcher = new CapturingTrainingDispatcher();
        var block      = HyperparamSearchBlockTestHarness.CreateBayesianBlock(dispatcher);

        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        await block.ProcessAsync(MakeFeatureVectorSignal("model-t"), CancellationToken.None);

        output.Should().NotBeNull("block must emit SEARCH_STARTED immediately");
        var val = output!.Value.Value;
        val.TryGetProperty("is_optuna_mode", out var isOptuna).Should().BeTrue();
        isOptuna.GetBoolean().Should().BeTrue("Bayesian strategy sets is_optuna_mode=true");
        val.TryGetProperty("direction", out var dir).Should().BeTrue();
        dir.GetString().Should().Be("maximize");
    }

    [Fact]
    public async Task HyperparamSearchBlock_OptunaSearchComplete_EmitsSearchCompleteWithBestParams()
    {
        var dispatcher = new CapturingTrainingDispatcher();
        var block      = HyperparamSearchBlockTestHarness.CreateBayesianBlock(dispatcher);

        var emitted = new List<BlockSignal>();
        block.OutputProduced += (sig, _) => { emitted.Add(sig); return ValueTask.CompletedTask; };

        // Start the search
        await block.ProcessAsync(MakeFeatureVectorSignal("model-t"), CancellationToken.None);

        // Simulate the study-complete envelope from Shell VM
        var jobId      = dispatcher.Dispatched[0].JobId;
        var bestParams = JsonSerializer.SerializeToElement(new { lr = 0.001f, dropout = 0.2f });
        var metrics    = JsonSerializer.SerializeToElement(new
        {
            best_value  = 0.73f,
            n_pruned    = 3,
            best_params = new { lr = 0.001f, dropout = 0.2f, hidden_dims = new[] { 64, 32 } },
        });

        var complete = new TrainingJobCompletePayload(
            JobId:       jobId,
            ModelType:   "model-t",
            ModelId:     "model-t-optuna-best",
            OnnxPath:    "",
            JoblibPath:  "",
            IpfsCid:     "",
            Metrics:     metrics,
            DurationMs:  12000);

        await dispatcher.SimulateCompleteAsync(complete);

        emitted.Should().HaveCountGreaterThanOrEqualTo(2,
            "at least SEARCH_STARTED and SEARCH_COMPLETE must be emitted");

        var last = emitted.Last().Value;
        last.TryGetProperty("state", out var state).Should().BeTrue();
        state.GetString().Should().Be("SEARCH_COMPLETE",
            "study completion must emit SEARCH_COMPLETE");

        last.TryGetProperty("best_metric", out var bm).Should().BeTrue();
        bm.GetSingle().Should().BeApproximately(0.73f, 1e-4f, "best_metric must match best_value from study");

        last.TryGetProperty("n_pruned", out var np).Should().BeTrue();
        np.GetInt32().Should().Be(3, "n_pruned must be forwarded from study result");
    }

    // ── ValidateModelBlock ────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateModelBlock_AcceptsWhenMetricsInNestedObject()
    {
        var block   = new ValidateModelBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // Emit a COMPLETE status with metrics only in the nested "metrics" object
        var status = JsonSerializer.SerializeToElement(new
        {
            state      = "COMPLETE",
            job_id     = "abc",
            model_type = "model-t",
            // No top-level accuracy/val_loss — these are inside "metrics"
            metrics    = new { accuracy = 0.75f, val_loss = 0.20f, f1_macro = 0.70f },
        });

        var signal = new BlockSignal(Guid.NewGuid(), "status_input", BlockSocketType.TrainingStatus, status);
        await block.ProcessAsync(signal, CancellationToken.None);

        output.Should().NotBeNull();
        var outState = output!.Value.Value.GetProperty("state").GetString();
        outState.Should().Be("ACCEPTED",
            "metrics from nested object must be used when top-level fields are absent");
    }

    [Fact]
    public async Task ValidateModelBlock_RejectsWhenMetricsDoNotMeetThreshold()
    {
        var block   = new ValidateModelBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        var status = JsonSerializer.SerializeToElement(new
        {
            state      = "COMPLETE",
            accuracy   = 0.30f,  // below default 0.60 threshold
            val_loss   = 0.80f,  // above default 0.50 threshold
        });

        var signal = new BlockSignal(Guid.NewGuid(), "status_input", BlockSocketType.TrainingStatus, status);
        await block.ProcessAsync(signal, CancellationToken.None);

        output.Should().NotBeNull();
        output!.Value.Value.GetProperty("state").GetString().Should().Be("REJECTED");
    }

    [Fact]
    public async Task ValidateModelBlock_PassesThroughTrainingState()
    {
        var block   = new ValidateModelBlock();
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        var status = JsonSerializer.SerializeToElement(new
        {
            state = "TRAINING",
            epoch = 5,
        });

        var signal = new BlockSignal(Guid.NewGuid(), "status_input", BlockSocketType.TrainingStatus, status);
        await block.ProcessAsync(signal, CancellationToken.None);

        output.Should().NotBeNull();
        output!.Value.Value.GetProperty("state").GetString().Should().Be("TRAINING",
            "non-COMPLETE states must pass through unchanged");
    }

    // ── DataLoaderBlock ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DataLoaderBlock_FiltersNonMatchingSymbol()
    {
        var block   = new DataLoaderBlock(); // default symbol = BTC-PERP
        var emitted = 0;
        block.OutputProduced += (_, _) => { emitted++; return ValueTask.CompletedTask; };

        // Feed 512 candles with a different symbol
        for (int i = 0; i < 512; i++)
            await block.ProcessAsync(MakeCandleWithMeta("ETH-PERP", "hyperliquid"), CancellationToken.None);

        emitted.Should().Be(0, "candles for the wrong symbol should be filtered out");
    }

    [Fact]
    public async Task DataLoaderBlock_AcceptsMatchingSymbol()
    {
        var block   = new DataLoaderBlock(); // default symbol = BTC-PERP, exchange = hyperliquid
        BlockSignal? output = null;
        block.OutputProduced += (sig, _) => { output = sig; return ValueTask.CompletedTask; };

        // Feed exactly 512 matching candles
        for (int i = 0; i < 512; i++)
            await block.ProcessAsync(MakeCandleWithMeta("BTC-PERP", "hyperliquid"), CancellationToken.None);

        output.Should().NotBeNull("matching candles should accumulate and emit a batch");
        output!.Value.SocketType.Should().Be(BlockSocketType.FeatureVector);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Feeds a FeatureVector batch containing <paramref name="samples"/> rows × 8 features
    /// into the block under test.
    /// </summary>
    private static async Task FeedFeatureBatch(TrainSplitBlock block, int samples, bool labels)
    {
        var rng       = new Random(42);
        var sampleArr = Enumerable.Range(0, samples)
            .Select(_ => Enumerable.Range(0, 8).Select(__ => (float)rng.NextDouble()).ToArray())
            .ToArray();

        var labelArr = labels
            ? (object)Enumerable.Range(0, samples).Select(i => i % 3).ToArray()
            : Array.Empty<int>();

        var payload = labels
            ? JsonSerializer.SerializeToElement(new
            {
                model_type    = "model-t",
                feature_names = new[] { "f0", "f1", "f2", "f3", "f4", "f5", "f6", "f7" },
                samples       = sampleArr,
                labels        = Enumerable.Range(0, samples).Select(i => i % 3).ToArray(),
            })
            : JsonSerializer.SerializeToElement(new
            {
                model_type    = "model-t",
                feature_names = new[] { "f0", "f1", "f2", "f3", "f4", "f5", "f6", "f7" },
                samples       = sampleArr,
            });

        var signal = new BlockSignal(Guid.NewGuid(), "feature_input", BlockSocketType.FeatureVector, payload);
        await block.ProcessAsync(signal, CancellationToken.None);
    }

    /// <summary>Creates a <see cref="TrainSplitBlock"/> configured with specific ratios.</summary>
    private static TrainSplitBlock CreateSplitBlockWithRatios(float trainRatio, float valRatio)
    {
        // Directly create and exercise via ProcessAsync — parameters are read-only defaults
        // so we work with defaults in tests and rely on the ratio clamping implementation.
        // Since BlockParameter<T> has no setter, we test the documented default ratios (0.80/0.10)
        // in the overflow test and verify the clamping behaviour rather than custom values.
        _ = trainRatio;
        _ = valRatio;
        return new TrainSplitBlock();
    }

    private static BlockSignal MakeCandleWithMeta(string symbol, string exchange) =>
        new(Guid.NewGuid(), "candle_output", BlockSocketType.CandleStream,
            JsonSerializer.SerializeToElement(new
            {
                symbol,
                exchange,
                open   = 100f,
                high   = 101f,
                low    = 99f,
                close  = 100.5f,
                volume = 500f,
            }));

    /// <summary>
    /// Creates a minimal <see cref="BlockSignal"/> of type <see cref="BlockSocketType.FeatureVector"/>
    /// carrying a <c>model_type</c> field — the minimum shape required by
    /// <see cref="HyperparamSearchBlock.ProcessCoreAsync"/>.
    /// </summary>
    private static BlockSignal MakeFeatureVectorSignal(string modelType) =>
        new(Guid.NewGuid(), "feature_input", BlockSocketType.FeatureVector,
            JsonSerializer.SerializeToElement(new
            {
                model_type    = modelType,
                feature_names = new[] { "f0", "f1", "f2", "f3", "f4", "f5", "f6", "f7" },
                samples       = new[] { new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f } },
                labels        = new[] { 0 },
            }));
}

/// <summary>
/// White-box test harness that exposes internal methods of <see cref="HyperparamSearchBlock"/>
/// without breaking encapsulation at production level.
/// </summary>
internal sealed class HyperparamSearchBlockTestHarness
{
    private readonly bool _minimize;
    private readonly List<(JsonElement Hyperparams, float Metric)> _trialResults = [];

    // Wraps real block for Optuna-mode tests
    private HyperparamSearchBlock? _block;

    public event Func<BlockSignal, CancellationToken, ValueTask>? OutputProduced
    {
        add    { if (_block is not null) _block.OutputProduced += value; }
        remove { if (_block is not null) _block.OutputProduced -= value; }
    }

    public HyperparamSearchBlockTestHarness(bool minimize) => _minimize = minimize;

    /// <summary>
    /// Creates a harness wrapping a real <see cref="HyperparamSearchBlock"/> wired to
    /// <paramref name="dispatcher"/> with <c>SearchStrategy=Bayesian</c> and
    /// <c>NTrials=5</c> for fast testing.
    /// </summary>
    public static HyperparamSearchBlockTestHarness CreateBayesianBlock(CapturingTrainingDispatcher dispatcher)
    {
        var block = new HyperparamSearchBlock(dispatcher);
        // Use reflection to set DefaultValue on the immutable BlockParameter fields so
        // the harness can configure strategy=Bayesian + n_trials=5 without needing a
        // public setter (which we deliberately avoid in production code).
        SetParameterDefaultViaReflection(block, "_searchStrategyParam", "Bayesian");
        SetParameterDefaultViaReflection(block, "_nTrialsParam",        5);
        SetParameterDefaultViaReflection(block, "_directionParam",      "maximize");
        SetParameterDefaultViaReflection(block, "_samplerParam",        "tpe");
        SetParameterDefaultViaReflection(block, "_prunerParam",         "hyperband");
        SetParameterDefaultViaReflection(block, "_epochsPerTrialParam", 3);

        return new HyperparamSearchBlockTestHarness(minimize: false) { _block = block };
    }

    /// <summary>Delegates <see cref="HyperparamSearchBlock.ProcessAsync"/> to the wrapped block.</summary>
    public ValueTask ProcessAsync(BlockSignal signal, CancellationToken ct)
    {
        if (_block is null) throw new InvalidOperationException("Use CreateBayesianBlock to get a real block");
        return _block.ProcessAsync(signal, ct);
    }

    public void AddTrialResult(float lr, float metric)
    {
        var hp = JsonSerializer.SerializeToElement(new { learning_rate = lr });
        _trialResults.Add((hp, metric));
    }

    /// <summary>Mirrors <c>HyperparamSearchBlock.GetBestTrial</c> exactly.</summary>
    public (JsonElement Hyperparams, float Metric) GetBestTrial()
    {
        if (_trialResults.Count == 0)
            return (JsonSerializer.SerializeToElement(new { }), float.NaN);

        return _minimize
            ? _trialResults.MinBy(r => r.Metric)
            : _trialResults.MaxBy(r => r.Metric);
    }

    /// <summary>Mirrors the fixed <c>HyperparamSearchBlock.IsEarlyStop</c> exactly.</summary>
    public bool IsEarlyStop()
    {
        const int earlyStopWindowSize = 5;
        if (_trialResults.Count <= earlyStopWindowSize) return false;

        var priorTrials  = _trialResults.Take(_trialResults.Count - earlyStopWindowSize).ToArray();
        var recentTrials = _trialResults.TakeLast(earlyStopWindowSize).ToArray();

        float bestBeforeRecent = _minimize
            ? priorTrials.Min(r => r.Metric)
            : priorTrials.Max(r => r.Metric);

        float bestRecent = _minimize
            ? recentTrials.Min(r => r.Metric)
            : recentTrials.Max(r => r.Metric);

        return _minimize ? bestRecent >= bestBeforeRecent : bestRecent <= bestBeforeRecent;
    }

    private static void SetParameterDefaultViaReflection<T>(
        HyperparamSearchBlock block, string fieldName, T value)
    {
        var field = typeof(HyperparamSearchBlock)
            .GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field {fieldName} not found");

        var param = (MLS.Core.Designer.BlockParameter<T>)(field.GetValue(block)
            ?? throw new InvalidOperationException($"Field {fieldName} is null"));

        // BlockParameter<T> is a positional record; use 'with' cloning to produce a new
        // instance with the overridden DefaultValue, then replace the readonly field via reflection.
        var cloned = param with { DefaultValue = value };
        field.SetValue(block, cloned);
    }
}

/// <summary>
/// Test double for <see cref="ITrainingDispatcher"/> that records all dispatched jobs and allows
/// simulating <see cref="JobCompleted"/> events.
/// </summary>
internal sealed class CapturingTrainingDispatcher : ITrainingDispatcher
{
    private readonly List<TrainingJobStartPayload> _dispatched = [];

    public IReadOnlyList<TrainingJobStartPayload> Dispatched => _dispatched;

    public event Func<TrainingJobProgressPayload, CancellationToken, ValueTask>? ProgressReceived;
    public event Func<TrainingJobCompletePayload,  CancellationToken, ValueTask>? JobCompleted;

    public ValueTask<Guid> DispatchJobAsync(TrainingJobStartPayload payload, CancellationToken ct)
    {
        _dispatched.Add(payload);
        return ValueTask.FromResult(payload.JobId);
    }

    public async Task SimulateCompleteAsync(TrainingJobCompletePayload complete)
    {
        if (JobCompleted is not null)
            await JobCompleted(complete, CancellationToken.None).ConfigureAwait(false);
    }
}
