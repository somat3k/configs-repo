namespace MLS.WorkflowRunner.Engine;

/// <summary>Result of a single workflow step for one symbol.</summary>
public sealed record StepResult(
    string Step,
    string Symbol,
    string Status,   // "ok" | "warn" | "error" | "skipped"
    long LatencyMs,
    string Value,
    string? Error = null);
