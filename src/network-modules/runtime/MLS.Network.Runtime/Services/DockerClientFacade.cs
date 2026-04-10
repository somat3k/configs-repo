using Docker.DotNet;
using Docker.DotNet.Models;

namespace MLS.Network.Runtime.Services;

/// <summary>Production implementation of <see cref="IDockerClientFacade"/> backed by <see cref="DockerClient"/>.</summary>
internal sealed class DockerClientFacade(DockerClient _client) : IDockerClientFacade
{
    /// <inheritdoc/>
    public Task<IList<ContainerListResponse>> ListContainersAsync(
        ContainersListParameters parameters, CancellationToken ct) =>
        _client.Containers.ListContainersAsync(parameters, ct);

    /// <inheritdoc/>
    public Task<bool> StartContainerAsync(string id, ContainerStartParameters parameters, CancellationToken ct) =>
        _client.Containers.StartContainerAsync(id, parameters, ct);

    /// <inheritdoc/>
    public Task<bool> StopContainerAsync(string id, ContainerStopParameters parameters, CancellationToken ct) =>
        _client.Containers.StopContainerAsync(id, parameters, ct);

    /// <inheritdoc/>
    public Task RestartContainerAsync(string id, ContainerRestartParameters parameters, CancellationToken ct) =>
        _client.Containers.RestartContainerAsync(id, parameters, ct);

    /// <inheritdoc/>
    public Task<MultiplexedStream> GetContainerLogsAsync(
        string id, bool tty, ContainerLogsParameters parameters, CancellationToken ct) =>
        _client.Containers.GetContainerLogsAsync(id, tty, parameters, ct);

    /// <inheritdoc/>
    public void Dispose() => _client.Dispose();
}
