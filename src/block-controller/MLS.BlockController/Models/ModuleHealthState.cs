namespace MLS.BlockController.Models;

/// <summary>
/// Ordered health states for a registered module.
/// Transitions flow according to the escalation model in <c>docs/bcg/health-escalation-model.md</c>.
/// </summary>
public enum ModuleHealthState
{
    /// <summary>Module registered but awaiting first successful heartbeat.</summary>
    Initializing,

    /// <summary>Heartbeats on time; no failures observed. Full routing eligibility.</summary>
    Healthy,

    /// <summary>Heartbeat jitter or soft failures detected. Fallback routing only.</summary>
    Degraded,

    /// <summary>Multiple missed heartbeats or hard failures. Not eligible for routing.</summary>
    Unstable,

    /// <summary>Operator-imposed; excluded from normal routing.</summary>
    Maintenance,

    /// <summary>Finishing in-flight work; no new workload assignments.</summary>
    Draining,

    /// <summary>Emergency stop; under investigation. No execution workloads.</summary>
    Quarantined,

    /// <summary>Missed heartbeat threshold exceeded or explicitly deregistered.</summary>
    Offline,
}
