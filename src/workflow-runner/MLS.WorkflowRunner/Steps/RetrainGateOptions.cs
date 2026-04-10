namespace MLS.WorkflowRunner.Steps;

/// <summary>Options that govern when a retrain is triggered.</summary>
public sealed record RetrainGateOptions
{
    /// <summary>Minimum model accuracy before retrain is triggered.</summary>
    [JsonPropertyName("min_accuracy")] public double MinAccuracy { get; init; } = 0.65;

    /// <summary>Minimum F1 score before retrain is triggered.</summary>
    [JsonPropertyName("min_f1")] public double MinF1 { get; init; } = 0.60;

    /// <summary>Max epochs without improvement before triggering retrain.</summary>
    [JsonPropertyName("max_epochs_no_improve")] public int MaxEpochsWithoutImprove { get; init; } = 20;

    /// <summary>How many cycles between forced retrains (0 = never force).</summary>
    [JsonPropertyName("retrain_interval_cycles")] public int RetrainIntervalCycles { get; init; } = 5;
}
