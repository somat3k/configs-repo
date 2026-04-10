using System.Runtime.CompilerServices;
using Docker.DotNet.Models;

namespace MLS.Network.Runtime.Services;

/// <summary>Docker-backed implementation of <see cref="IModuleRuntimeService"/>.</summary>
public sealed class ModuleRuntimeService(
    IDockerClientFacade _docker,
    ILogger<ModuleRuntimeService> _logger) : IModuleRuntimeService, IDisposable
{
    /// <inheritdoc/>
    public async Task<ModuleStatus> GetStatusAsync(string moduleName, CancellationToken ct)
    {
        try
        {
            var containers = await _docker.ListContainersAsync(
                new ContainersListParameters
                {
                    All    = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool>
                            { [$"{RuntimeConstants.MlsModuleLabel}={moduleName}"] = true }
                    }
                }, ct).ConfigureAwait(false);

            var container = containers.FirstOrDefault();
            if (container is null) return NotFound(moduleName);

            var state = container.State == "running" ? ModuleState.Running : ModuleState.Stopped;
            var ports  = container.Ports.Select(p => $"{p.PublicPort}:{p.PrivatePort}").ToList();
            return new ModuleStatus(moduleName, container.ID, state, container.Image,
                container.Created, ports.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Docker unavailable — returning NotFound for {Module}", moduleName);
            return NotFound(moduleName);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleStatus>> ListModulesAsync(CancellationToken ct)
    {
        try
        {
            var containers = await _docker.ListContainersAsync(
                new ContainersListParameters
                {
                    All     = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool>
                            { [RuntimeConstants.MlsModuleLabel] = true }
                    }
                }, ct).ConfigureAwait(false);

            return containers.Select(c =>
            {
                var state = c.State == "running" ? ModuleState.Running : ModuleState.Stopped;
                var label = c.Labels.TryGetValue(RuntimeConstants.MlsModuleLabel, out var n) ? n : c.Names.FirstOrDefault() ?? c.ID;
                var ports  = c.Ports.Select(p => $"{p.PublicPort}:{p.PrivatePort}").ToList();
                return new ModuleStatus(label, c.ID, state, c.Image, c.Created, ports.AsReadOnly());
            }).ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Docker unavailable — returning empty list");
            return Array.Empty<ModuleStatus>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StartModuleAsync(string moduleName, CancellationToken ct)
    {
        try
        {
            var status = await GetStatusAsync(moduleName, ct).ConfigureAwait(false);
            if (status.State == ModuleState.NotFound) return false;
            return await _docker.StartContainerAsync(status.ContainerId,
                new ContainerStartParameters(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start {Module}", moduleName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StopModuleAsync(string moduleName, CancellationToken ct)
    {
        try
        {
            var status = await GetStatusAsync(moduleName, ct).ConfigureAwait(false);
            if (status.State == ModuleState.NotFound) return false;
            return await _docker.StopContainerAsync(status.ContainerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 10u }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop {Module}", moduleName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RestartModuleAsync(string moduleName, CancellationToken ct)
    {
        try
        {
            var status = await GetStatusAsync(moduleName, ct).ConfigureAwait(false);
            if (status.State == ModuleState.NotFound) return false;
            await _docker.RestartContainerAsync(status.ContainerId,
                new ContainerRestartParameters { WaitBeforeKillSeconds = 10u }, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restart {Module}", moduleName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ContainerLog> StreamLogsAsync(
        string moduleName,
        int tailLines,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ModuleStatus status;
        try
        {
            status = await GetStatusAsync(moduleName, ct).ConfigureAwait(false);
        }
        catch
        {
            yield break;
        }

        if (status.State == ModuleState.NotFound) yield break;

        Docker.DotNet.MultiplexedStream logStream;
        try
        {
            logStream = await _docker.GetContainerLogsAsync(status.ContainerId,
                false,
                new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Tail       = tailLines.ToString(),
                    Timestamps = true,
                }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stream logs for {Module}", moduleName);
            yield break;
        }

        using (logStream)
        {
            var buf = new byte[4096];
            while (!ct.IsCancellationRequested)
            {
                var result = await logStream.ReadOutputAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
                if (result.EOF) break;
                var text = System.Text.Encoding.UTF8.GetString(buf, 0, result.Count);
                yield return new ContainerLog(DateTimeOffset.UtcNow,
                    result.Target == Docker.DotNet.MultiplexedStream.TargetStream.StandardError ? "stderr" : "stdout",
                    text);
            }
        }
    }

    private static ModuleStatus NotFound(string name) =>
        new(name, string.Empty, ModuleState.NotFound, string.Empty, null, Array.Empty<string>());

    /// <inheritdoc/>
    public void Dispose() => _docker.Dispose();
}
