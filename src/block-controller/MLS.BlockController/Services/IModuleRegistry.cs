using MLS.BlockController.Models;

namespace MLS.BlockController.Services;

/// <summary>
/// Maintains the registry of all live modules, their endpoints, and heartbeat timestamps.
/// </summary>
public interface IModuleRegistry
{
    /// <summary>Register a new module and return its registration record.</summary>
    Task<ModuleRegistration> RegisterAsync(RegisterModuleRequest request, CancellationToken ct = default);

    /// <summary>Deregister a module by ID. No-op if not found.</summary>
    Task DeregisterAsync(Guid moduleId, CancellationToken ct = default);

    /// <summary>Return all currently registered modules.</summary>
    Task<IReadOnlyList<ModuleRegistration>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Return a single module registration, or <see langword="null"/> if not found.</summary>
    Task<ModuleRegistration?> GetByIdAsync(Guid moduleId, CancellationToken ct = default);

    /// <summary>Update the last-heartbeat timestamp for a module.</summary>
    Task UpdateHeartbeatAsync(Guid moduleId, DateTimeOffset heartbeatTime, CancellationToken ct = default);
}
