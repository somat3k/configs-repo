namespace MLS.WorkflowRunner.Engine;

/// <summary>Aggregated result of one complete workflow cycle.</summary>
public sealed record CycleResult(
    int Cycle,
    int TotalCycles,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyList<StepResult> StepResults,
    string Status);  // "completed" | "failed"
