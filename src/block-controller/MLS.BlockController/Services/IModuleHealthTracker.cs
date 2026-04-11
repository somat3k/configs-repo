using MLS.BlockController.Models;

namespace MLS.BlockController.Services;

/// <summary>
/// Tracks the health state of every registered module and manages health state transitions.
/// Emits broadcast envelope events on every state change.
/// </summary>
public interface IModuleHealthTracker
{
    /// <summary>Set the initial health state for a newly registered module.</summary>
    Task InitializeAsync(Guid moduleId, string moduleName, CancellationToken ct = default);

    /// <summary>
    /// Record that a valid heartbeat has been received.
    /// Transitions <see cref="ModuleHealthState.Initializing"/> → <see cref="ModuleHealthState.Healthy"/>
    /// and <see cref="ModuleHealthState.Degraded"/> → <see cref="ModuleHealthState.Healthy"/> on recovery.
    /// </summary>
    Task RecordHeartbeatAsync(Guid moduleId, CancellationToken ct = default);

    /// <summary>
    /// Record a missed heartbeat and escalate the health state if thresholds are exceeded.
    /// Escalation order: Healthy → Degraded → Unstable → Offline.
    /// </summary>
    Task RecordMissedHeartbeatAsync(Guid moduleId, CancellationToken ct = default);

    /// <summary>
    /// Explicitly transition a module to a target state (e.g. Draining, Maintenance, Quarantined).
    /// Used for operator-driven state changes.
    /// </summary>
    Task TransitionStateAsync(Guid moduleId, ModuleHealthState targetState, string? reason = null, CancellationToken ct = default);

    /// <summary>Return the current health state for a module, or <see langword="null"/> if unknown.</summary>
    Task<ModuleHealthState?> GetHealthStateAsync(Guid moduleId, CancellationToken ct = default);

    /// <summary>Return all modules currently in a given health state.</summary>
    Task<IReadOnlyList<Guid>> GetModulesInStateAsync(ModuleHealthState state, CancellationToken ct = default);

    /// <summary>Remove a module's health tracking record. Called on deregistration.</summary>
    Task RemoveAsync(Guid moduleId, CancellationToken ct = default);
}
