namespace MLS.Core.Kernels;

/// <summary>
/// Performance objectives declared for a kernel family.
/// </summary>
/// <param name="P95Latency">Target p95 completion latency when applicable.</param>
/// <param name="FirstPartialP95">Target p95 first-partial latency for streaming kernels.</param>
/// <param name="RequiresProgressEvents">Whether progress events are mandatory during long execution.</param>
public sealed record KernelPerformanceBudget(
    TimeSpan? P95Latency,
    TimeSpan? FirstPartialP95,
    bool RequiresProgressEvents);
