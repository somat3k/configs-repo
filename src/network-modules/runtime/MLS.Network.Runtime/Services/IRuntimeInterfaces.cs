using System.Runtime.CompilerServices;

namespace MLS.Network.Runtime.Services;

/// <summary>State of a Docker-managed module container.</summary>
public enum ModuleState { Running, Stopped, NotFound }

/// <summary>Runtime status of a module container.</summary>
public sealed record ModuleStatus(
    string ModuleName,
    string ContainerId,
    ModuleState State,
    string Image,
    DateTimeOffset? StartedAt,
    IReadOnlyList<string> Ports);

/// <summary>A single log line from a container stream.</summary>
public sealed record ContainerLog(DateTimeOffset Timestamp, string Stream, string Message);

/// <summary>Service interface for Docker container lifecycle management.</summary>
public interface IModuleRuntimeService
{
    /// <summary>Gets the runtime status of a named module container.</summary>
    Task<ModuleStatus> GetStatusAsync(string moduleName, CancellationToken ct);

    /// <summary>Lists the status of all MLS-labelled module containers.</summary>
    Task<IReadOnlyList<ModuleStatus>> ListModulesAsync(CancellationToken ct);

    /// <summary>Starts a named module container. Returns <see langword="true"/> on success.</summary>
    Task<bool> StartModuleAsync(string moduleName, CancellationToken ct);

    /// <summary>Stops a named module container. Returns <see langword="true"/> on success.</summary>
    Task<bool> StopModuleAsync(string moduleName, CancellationToken ct);

    /// <summary>Restarts a named module container. Returns <see langword="true"/> on success.</summary>
    Task<bool> RestartModuleAsync(string moduleName, CancellationToken ct);

    /// <summary>Streams log lines from a named module container.</summary>
    IAsyncEnumerable<ContainerLog> StreamLogsAsync(string moduleName, int tailLines, CancellationToken ct);
}
