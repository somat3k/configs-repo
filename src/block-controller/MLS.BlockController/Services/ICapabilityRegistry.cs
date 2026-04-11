using MLS.BlockController.Models;

namespace MLS.BlockController.Services;

/// <summary>
/// Stores and queries module capability declarations.
/// The registry is the single source of truth for what each module can do.
/// </summary>
public interface ICapabilityRegistry
{
    /// <summary>
    /// Register or overwrite a module's capability record.
    /// Emits <c>MODULE_CAPABILITY_UPDATED</c> on every successful call.
    /// </summary>
    Task RegisterAsync(CapabilityRecord record, CancellationToken ct = default);

    /// <summary>
    /// Return all capability records whose <c>OperationTypes</c> contains
    /// <paramref name="operationType"/>, ordered descending by capability match score.
    /// Returns an empty list when no module is capable.
    /// </summary>
    Task<IReadOnlyList<CapabilityRecord>> ResolveByOperationAsync(
        string operationType, CancellationToken ct = default);

    /// <summary>Return the capability record for a single module, or <see langword="null"/> if not found.</summary>
    Task<CapabilityRecord?> GetAsync(Guid moduleId, CancellationToken ct = default);

    /// <summary>
    /// Remove a module's capability record from the registry.
    /// Called on heartbeat timeout or explicit deregistration.
    /// No-op if the module is not registered.
    /// </summary>
    Task EvictAsync(Guid moduleId, CancellationToken ct = default);
}
