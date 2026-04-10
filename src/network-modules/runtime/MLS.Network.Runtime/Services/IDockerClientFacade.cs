using Docker.DotNet.Models;

namespace MLS.Network.Runtime.Services;

/// <summary>
/// Thin abstraction over <see cref="Docker.DotNet.DockerClient"/> to enable unit testing
/// of <see cref="ModuleRuntimeService"/> without a live Docker daemon.
/// </summary>
public interface IDockerClientFacade : IDisposable
{
    /// <summary>Lists containers matching the supplied parameters.</summary>
    Task<IList<ContainerListResponse>> ListContainersAsync(ContainersListParameters parameters, CancellationToken ct);

    /// <summary>Starts a container by ID.</summary>
    Task<bool> StartContainerAsync(string id, ContainerStartParameters parameters, CancellationToken ct);

    /// <summary>Stops a container by ID.</summary>
    Task<bool> StopContainerAsync(string id, ContainerStopParameters parameters, CancellationToken ct);

    /// <summary>Restarts a container by ID.</summary>
    Task RestartContainerAsync(string id, ContainerRestartParameters parameters, CancellationToken ct);

    /// <summary>Gets a log stream for a container.</summary>
    Task<Docker.DotNet.MultiplexedStream> GetContainerLogsAsync(
        string id, bool tty, ContainerLogsParameters parameters, CancellationToken ct);
}
