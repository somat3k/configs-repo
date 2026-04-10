using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MLS.Network.NetworkMask.Services;

/// <summary>Thread-safe in-memory implementation of <see cref="INetworkMaskService"/>.</summary>
public sealed class NetworkMaskService(
    IOptions<NetworkMaskConfig> _config,
    ILogger<NetworkMaskService> _logger) : INetworkMaskService
{
    private readonly ConcurrentDictionary<string, EndpointInfo> _endpoints = SeedEndpoints(_config.Value);

    private static ConcurrentDictionary<string, EndpointInfo> SeedEndpoints(NetworkMaskConfig cfg)
    {
        var dict = new ConcurrentDictionary<string, EndpointInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in cfg.KnownEndpoints)
        {
            var key  = Key(r.ModuleName, r.Environment);
            var info = new EndpointInfo(r.ModuleName, r.Environment, r.HttpUrl, r.WsUrl,
                DateTimeOffset.UtcNow, r.Tags);
            dict[key] = info;
        }
        return dict;
    }

    /// <inheritdoc/>
    public Task RegisterEndpointAsync(EndpointRegistration registration, CancellationToken ct)
    {
        var key  = Key(registration.ModuleName, registration.Environment);
        var info = new EndpointInfo(registration.ModuleName, registration.Environment,
            registration.HttpUrl, registration.WsUrl, DateTimeOffset.UtcNow, registration.Tags);
        _endpoints[key] = info;
        _logger.LogInformation("Registered endpoint {Module} ({Env})", registration.ModuleName, registration.Environment);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<EndpointInfo?> ResolveEndpointAsync(string moduleName, string environment, CancellationToken ct) =>
        Task.FromResult(_endpoints.TryGetValue(Key(moduleName, environment), out var info) ? info : null);

    /// <inheritdoc/>
    public async IAsyncEnumerable<EndpointInfo> ListEndpointsAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var info in _endpoints.Values)
        {
            ct.ThrowIfCancellationRequested();
            yield return info;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public Task<bool> RemoveEndpointAsync(string moduleName, string environment, CancellationToken ct) =>
        Task.FromResult(_endpoints.TryRemove(Key(moduleName, environment), out _));

    /// <inheritdoc/>
    public async Task<string?> ResolveUrlAsync(string moduleName, string environment, string path, CancellationToken ct)
    {
        var info = await ResolveEndpointAsync(moduleName, environment, ct).ConfigureAwait(false);
        if (info is null) return null;
        var base_ = info.HttpUrl.TrimEnd('/');
        var tail  = path.StartsWith('/') ? path : $"/{path}";
        return $"{base_}{tail}";
    }

    private static string Key(string module, string env) =>
        $"{module.ToLowerInvariant()}:{env.ToLowerInvariant()}";
}
