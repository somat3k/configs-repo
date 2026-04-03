using System.Collections.Concurrent;
using MLS.BlockController.Models;

namespace MLS.BlockController.Services;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IModuleRegistry"/>.
/// </summary>
public sealed class InMemoryModuleRegistry : IModuleRegistry
{
    private readonly ConcurrentDictionary<Guid, ModuleRegistration> _modules = new();

    /// <inheritdoc/>
    public Task<ModuleRegistration> RegisterAsync(RegisterModuleRequest request, CancellationToken ct = default)
    {
        var registration = new ModuleRegistration(
            ModuleId: Guid.NewGuid(),
            ModuleName: request.ModuleName,
            EndpointHttp: request.EndpointHttp,
            EndpointWs: request.EndpointWs,
            Capabilities: request.Capabilities,
            Version: request.Version,
            RegisteredAt: DateTimeOffset.UtcNow,
            LastHeartbeat: DateTimeOffset.UtcNow);

        _modules[registration.ModuleId] = registration;
        return Task.FromResult(registration);
    }

    /// <inheritdoc/>
    public Task DeregisterAsync(Guid moduleId, CancellationToken ct = default)
    {
        _modules.TryRemove(moduleId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModuleRegistration>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ModuleRegistration>>(_modules.Values.ToList());

    /// <inheritdoc/>
    public Task<ModuleRegistration?> GetByIdAsync(Guid moduleId, CancellationToken ct = default)
    {
        _modules.TryGetValue(moduleId, out var reg);
        return Task.FromResult<ModuleRegistration?>(reg);
    }

    /// <inheritdoc/>
    public Task UpdateHeartbeatAsync(Guid moduleId, DateTimeOffset heartbeatTime, CancellationToken ct = default)
    {
        if (_modules.TryGetValue(moduleId, out var existing))
        {
            _modules[moduleId] = existing with { LastHeartbeat = heartbeatTime };
        }

        return Task.CompletedTask;
    }
}
