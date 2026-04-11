using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MLS.BlockController.Models;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.BlockController;

namespace MLS.BlockController.Services;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IModuleHealthTracker"/>.
/// <para>
/// Escalation rules (per <c>docs/bcg/health-escalation-model.md</c>):
/// <list type="bullet">
///   <item>1 missed heartbeat within window → Degraded</item>
///   <item>2 consecutive missed heartbeats → Unstable</item>
///   <item>3+ consecutive missed heartbeats → Offline</item>
///   <item>Any valid heartbeat from Degraded → Healthy (recovery)</item>
/// </list>
/// </para>
/// </summary>
public sealed class ModuleHealthTracker(
    IMessageRouter _router,
    ILogger<ModuleHealthTracker> _logger) : IModuleHealthTracker
{
    private const string ControllerId = "block-controller";

    private sealed record HealthEntry(
        string ModuleName,
        ModuleHealthState State,
        int ConsecutiveMisses,
        DateTimeOffset LastHeartbeat);

    private readonly ConcurrentDictionary<Guid, HealthEntry> _entries = new();

    /// <inheritdoc/>
    public Task InitializeAsync(Guid moduleId, string moduleName, CancellationToken ct = default)
    {
        _entries[moduleId] = new HealthEntry(
            ModuleName: moduleName,
            State: ModuleHealthState.Initializing,
            ConsecutiveMisses: 0,
            LastHeartbeat: DateTimeOffset.UtcNow);

        _logger.LogDebug("Health initialized for module {Name} ({Id}) → Initializing", moduleName, moduleId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task RecordHeartbeatAsync(Guid moduleId, CancellationToken ct = default)
    {
        if (!_entries.TryGetValue(moduleId, out var entry))
            return;

        var previous = entry.State;
        var next = entry.State switch
        {
            ModuleHealthState.Initializing => ModuleHealthState.Healthy,
            ModuleHealthState.Degraded     => ModuleHealthState.Healthy,
            ModuleHealthState.Unstable     => ModuleHealthState.Degraded, // gradual recovery
            _                              => entry.State,
        };

        _entries[moduleId] = entry with
        {
            State = next,
            ConsecutiveMisses = 0,
            LastHeartbeat = DateTimeOffset.UtcNow,
        };

        if (next != previous)
        {
            _logger.LogInformation("Health transition {Name} ({Id}): {Prev} → {Next}",
                entry.ModuleName, moduleId, previous, next);

            await BroadcastTransitionAsync(moduleId, entry.ModuleName, previous, next,
                reason: "heartbeat received", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task RecordMissedHeartbeatAsync(Guid moduleId, CancellationToken ct = default)
    {
        if (!_entries.TryGetValue(moduleId, out var entry))
            return;

        var misses = entry.ConsecutiveMisses + 1;
        var previous = entry.State;

        var next = (previous, misses) switch
        {
            // Already offline / maintenance / quarantine — do not escalate further
            (ModuleHealthState.Offline, _)       => ModuleHealthState.Offline,
            (ModuleHealthState.Maintenance, _)   => ModuleHealthState.Maintenance,
            (ModuleHealthState.Quarantined, _)   => ModuleHealthState.Quarantined,
            (ModuleHealthState.Draining, _)      => ModuleHealthState.Draining,

            // Escalation ladder
            (_, >= 3)                            => ModuleHealthState.Offline,
            (_, 2)                               => ModuleHealthState.Unstable,
            (_, 1) when previous == ModuleHealthState.Healthy      => ModuleHealthState.Degraded,
            (_, 1) when previous == ModuleHealthState.Initializing => ModuleHealthState.Degraded,
            _                                    => previous,
        };

        _entries[moduleId] = entry with { State = next, ConsecutiveMisses = misses };

        if (next != previous)
        {
            _logger.LogWarning("Health escalation {Name} ({Id}): {Prev} → {Next} (miss #{Miss})",
                entry.ModuleName, moduleId, previous, next, misses);

            await BroadcastTransitionAsync(moduleId, entry.ModuleName, previous, next,
                reason: $"missed heartbeat #{misses}", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task TransitionStateAsync(
        Guid moduleId, ModuleHealthState targetState, string? reason = null, CancellationToken ct = default)
    {
        if (!_entries.TryGetValue(moduleId, out var entry))
            return;

        var previous = entry.State;
        if (previous == targetState)
            return;

        _entries[moduleId] = entry with { State = targetState, ConsecutiveMisses = 0 };

        _logger.LogInformation("Explicit health transition {Name} ({Id}): {Prev} → {Next} | reason={Reason}",
            entry.ModuleName, moduleId, previous, targetState, reason ?? "operator");

        await BroadcastTransitionAsync(moduleId, entry.ModuleName, previous, targetState, reason, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<ModuleHealthState?> GetHealthStateAsync(Guid moduleId, CancellationToken ct = default)
    {
        ModuleHealthState? state = _entries.TryGetValue(moduleId, out var entry) ? entry.State : null;
        return Task.FromResult(state);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Guid>> GetModulesInStateAsync(ModuleHealthState state, CancellationToken ct = default)
    {
        var result = _entries
            .Where(kvp => kvp.Value.State == state)
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult<IReadOnlyList<Guid>>(result);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(Guid moduleId, CancellationToken ct = default)
    {
        _entries.TryRemove(moduleId, out _);
        return Task.CompletedTask;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task BroadcastTransitionAsync(
        Guid moduleId, string moduleName,
        ModuleHealthState previous, ModuleHealthState next,
        string? reason, CancellationToken ct)
    {
        var eventType = next switch
        {
            ModuleHealthState.Healthy      => MessageTypes.ModuleRecovered,
            ModuleHealthState.Degraded     => MessageTypes.ModuleDegraded,
            ModuleHealthState.Draining     => MessageTypes.ModuleDrained,
            ModuleHealthState.Offline      => MessageTypes.ModuleOffline,
            ModuleHealthState.Maintenance  => MessageTypes.ModuleMaintenance,
            ModuleHealthState.Quarantined  => MessageTypes.ModuleQuarantined,
            _                              => MessageTypes.ModuleCapabilityUpdated,
        };

        var payload = new ModuleHealthChangedPayload(
            ModuleId: moduleId,
            ModuleName: moduleName,
            PreviousState: previous.ToString(),
            CurrentState: next.ToString(),
            Reason: reason,
            Timestamp: DateTimeOffset.UtcNow);

        var envelope = EnvelopePayload.Create(eventType, ControllerId, payload);
        await _router.BroadcastAsync(envelope, ct).ConfigureAwait(false);
    }
}
