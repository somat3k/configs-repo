using System.Runtime.CompilerServices;

namespace MLS.Network.NetworkMask.Services;

/// <summary>Endpoint registration payload.</summary>
public sealed record EndpointRegistration(
    string ModuleName,
    string Environment,
    string HttpUrl,
    string WsUrl,
    string[] Tags);

/// <summary>Registered endpoint information.</summary>
public sealed record EndpointInfo(
    string ModuleName,
    string Environment,
    string HttpUrl,
    string WsUrl,
    DateTimeOffset RegisteredAt,
    string[] Tags);

/// <summary>Service interface for endpoint registry and resolution.</summary>
public interface INetworkMaskService
{
    /// <summary>Registers or updates a module endpoint.</summary>
    Task RegisterEndpointAsync(EndpointRegistration registration, CancellationToken ct);

    /// <summary>Resolves an endpoint for a module in the given environment.</summary>
    Task<EndpointInfo?> ResolveEndpointAsync(string moduleName, string environment, CancellationToken ct);

    /// <summary>Streams all registered endpoints.</summary>
    IAsyncEnumerable<EndpointInfo> ListEndpointsAsync(CancellationToken ct);

    /// <summary>Removes an endpoint registration.</summary>
    Task<bool> RemoveEndpointAsync(string moduleName, string environment, CancellationToken ct);

    /// <summary>Resolves the full URL for a path on a module's HTTP endpoint.</summary>
    Task<string?> ResolveUrlAsync(string moduleName, string environment, string path, CancellationToken ct);
}
