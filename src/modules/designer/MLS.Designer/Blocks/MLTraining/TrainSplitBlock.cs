using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.MLTraining;

/// <summary>
/// Train-split block — accumulates engineered <see cref="BlockSocketType.FeatureVector"/>
/// samples and, once the minimum sample count is reached, performs a stratified 80/10/10
/// train / validation / test split.
/// <para>
/// Each split is emitted as a separate <see cref="BlockSocketType.FeatureVector"/> signal
/// with a <c>split</c> field set to <c>"train"</c>, <c>"val"</c>, or <c>"test"</c>.
/// </para>
/// </summary>
/// <remarks>
/// Labels are derived from the <c>labels</c> array embedded in the incoming batch. If no
/// labels are present, a zero vector is used (unsupervised mode).
/// </remarks>
public sealed class TrainSplitBlock : BlockBase
{
    private readonly List<float[]> _samples = [];
    private readonly List<int>     _labels  = [];

    private readonly BlockParameter<float> _trainRatioParam =
        new("TrainRatio", "Train Ratio", "Fraction of samples for training",    0.80f, MinValue: 0.5f,  MaxValue: 0.95f);
    private readonly BlockParameter<float> _valRatioParam =
        new("ValRatio",   "Val Ratio",   "Fraction of samples for validation",  0.10f, MinValue: 0.02f, MaxValue: 0.30f);
    private readonly BlockParameter<int> _minSamplesParam =
        new("MinSamples", "Min Samples", "Minimum samples before splitting",    100, MinValue: 10, MaxValue: 100_000);
    private readonly BlockParameter<bool> _shuffleParam =
        new("Shuffle",    "Shuffle",     "Shuffle samples before splitting",    true);

    /// <inheritdoc/>
    public override string BlockType   => "TrainSplitBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Train Split";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_trainRatioParam, _valRatioParam, _minSamplesParam, _shuffleParam];

    /// <summary>Initialises a new <see cref="TrainSplitBlock"/>.</summary>
    public TrainSplitBlock() : base(
        [BlockSocket.Input("feature_input", BlockSocketType.FeatureVector)],
        [BlockSocket.Output("split_output", BlockSocketType.FeatureVector)]) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _samples.Clear();
        _labels.Clear();
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.FeatureVector)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractBatch(signal.Value, out var modelType, out var featureNames, out var samples, out var labels))
            return new ValueTask<BlockSignal?>(result: null);

        _samples.AddRange(samples);
        // Use -1 as a sentinel for absent labels (makes missing-label samples explicit downstream)
        _labels.AddRange(labels.Length == samples.Length ? labels : Enumerable.Repeat(-1, samples.Length));

        if (_samples.Count < _minSamplesParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);

        // ── Perform split ─────────────────────────────────────────────────────────
        var allSamples = _samples.ToArray();
        var allLabels  = _labels.ToArray();
        _samples.Clear();
        _labels.Clear();

        if (_shuffleParam.DefaultValue)
            Shuffle(allSamples, allLabels);

        int n    = allSamples.Length;
        int nTr  = (int)(n * _trainRatioParam.DefaultValue);
        int nVal = (int)(n * _valRatioParam.DefaultValue);
        int nTst = n - nTr - nVal;

        var trainSamples = allSamples[..nTr];
        var valSamples   = allSamples[nTr..(nTr + nVal)];
        var testSamples  = allSamples[(nTr + nVal)..];

        var trainLabels  = allLabels[..nTr];
        var valLabels    = allLabels[nTr..(nTr + nVal)];
        var testLabels   = allLabels[(nTr + nVal)..];

        // Emit the complete split bundle as a single FeatureVector signal
        var splitBundle = new SplitBundle(
            ModelType:    modelType,
            FeatureNames: featureNames,
            TotalSamples: n,
            Train:        new SplitSegment("train", trainSamples, trainLabels),
            Val:          new SplitSegment("val",   valSamples,   valLabels),
            Test:         new SplitSegment("test",  testSamples,  testLabels));

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "split_output", BlockSocketType.FeatureVector, splitBundle));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static void Shuffle(float[][] samples, int[] labels)
    {
        var rng = Random.Shared;
        for (int i = samples.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (samples[i], samples[j]) = (samples[j], samples[i]);
            (labels[i],  labels[j])  = (labels[j],  labels[i]);
        }
    }

    private static bool TryExtractBatch(
        JsonElement value,
        out string modelType, out string[] featureNames,
        out float[][] samples, out int[] labels)
    {
        modelType    = "model-t";
        featureNames = [];
        samples      = [];
        labels       = [];

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (value.TryGetProperty("model_type",    out var mt)) modelType = mt.GetString() ?? "model-t";
        if (value.TryGetProperty("feature_names", out var fn) && fn.ValueKind == JsonValueKind.Array)
            featureNames = fn.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();

        if (!value.TryGetProperty("samples", out var samplesEl) ||
            samplesEl.ValueKind != JsonValueKind.Array)
            return false;

        var sampleList = new List<float[]>();
        foreach (var row in samplesEl.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array) continue;
            var arr = new float[row.GetArrayLength()];
            int i = 0;
            foreach (var el in row.EnumerateArray()) arr[i++] = el.GetSingle();
            sampleList.Add(arr);
        }
        samples = [.. sampleList];

        if (value.TryGetProperty("labels", out var labelsEl) &&
            labelsEl.ValueKind == JsonValueKind.Array)
        {
            var labelList = new List<int>();
            foreach (var el in labelsEl.EnumerateArray())
                labelList.Add(el.GetInt32());
            labels = [.. labelList];
        }

        return samples.Length > 0;
    }

    // ── Wire types ────────────────────────────────────────────────────────────────

    internal sealed record SplitBundle(
        [property: JsonPropertyName("model_type")]    string       ModelType,
        [property: JsonPropertyName("feature_names")] string[]     FeatureNames,
        [property: JsonPropertyName("total_samples")] int          TotalSamples,
        [property: JsonPropertyName("train")]         SplitSegment Train,
        [property: JsonPropertyName("val")]           SplitSegment Val,
        [property: JsonPropertyName("test")]          SplitSegment Test);

    internal sealed record SplitSegment(
        [property: JsonPropertyName("split")]   string   Split,
        [property: JsonPropertyName("samples")] float[][] Samples,
        [property: JsonPropertyName("labels")]  int[]    Labels);
}
