using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MLS.BlockController.Models;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.BlockController;

namespace MLS.BlockController.Services;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ICapabilityRegistry"/>.
/// Broadcasts <c>MODULE_CAPABILITY_UPDATED</c> on every register or update.
/// </summary>
public sealed class InMemoryCapabilityRegistry(
    IMessageRouter _router,
    ILogger<InMemoryCapabilityRegistry> _logger) : ICapabilityRegistry
{
    private const string ModuleId = "block-controller";

    private readonly ConcurrentDictionary<Guid, CapabilityRecord> _records = new();

    /// <inheritdoc/>
    public async Task RegisterAsync(CapabilityRecord record, CancellationToken ct = default)
    {
        var isUpdate = _records.ContainsKey(record.ModuleId);
        _records[record.ModuleId] = record;

        _logger.LogInformation(
            "{Action} capability for module {Name} ({Id}) — ops=[{Ops}]",
            isUpdate ? "Updated" : "Registered",
            record.ModuleName,
            record.ModuleId,
            string.Join(", ", record.OperationTypes));

        var payload = new ModuleCapabilityUpdatedPayload(
            ModuleId: record.ModuleId,
            ModuleName: record.ModuleName,
            OperationTypes: record.OperationTypes,
            Version: record.Version,
            Timestamp: DateTimeOffset.UtcNow);

        var envelope = EnvelopePayload.Create(
            MessageTypes.ModuleCapabilityUpdated, ModuleId, payload);

        await _router.BroadcastAsync(envelope, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<CapabilityRecord>> ResolveByOperationAsync(
        string operationType, CancellationToken ct = default)
    {
        var matches = _records.Values
            .Where(r => r.OperationTypes.Contains(operationType, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(ComputeMatchScore)
            .ToList();

        return Task.FromResult<IReadOnlyList<CapabilityRecord>>(matches);
    }

    /// <inheritdoc/>
    public Task<CapabilityRecord?> GetAsync(Guid moduleId, CancellationToken ct = default)
    {
        _records.TryGetValue(moduleId, out var record);
        return Task.FromResult<CapabilityRecord?>(record);
    }

    /// <inheritdoc/>
    public Task EvictAsync(Guid moduleId, CancellationToken ct = default)
    {
        if (_records.TryRemove(moduleId, out var removed))
        {
            _logger.LogInformation("Capability record evicted for module {Name} ({Id})",
                removed.ModuleName, moduleId);
        }

        return Task.CompletedTask;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Simple capability match score used to order resolution results.
    /// Exact match to any declared operation type scores maximum; broader declarations score lower.
    /// </summary>
    private static int ComputeMatchScore(CapabilityRecord record)
    {
        // Fewer declared operations → tighter specialist match → higher score
        // (a module that declares only INFERENCE_REQUEST is a better match than one declaring everything)
        return Math.Max(0, 100 - record.OperationTypes.Count);
    }
}
