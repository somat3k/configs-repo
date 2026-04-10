using System.Runtime.CompilerServices;

namespace MLS.Network.ContainerRegistry.Services;

/// <summary>Request payload for registering a container image.</summary>
public sealed record RegisterImageRequest(string Name, string Tag, string Registry, string? Digest);

/// <summary>A registered container image.</summary>
public sealed record ContainerImage(
    Guid Id,
    string Name,
    string Tag,
    string Registry,
    string? Digest,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? LastHealthAt,
    bool IsHealthy);

/// <summary>Result of a health check probe.</summary>
public sealed record HealthCheckResult(
    Guid ImageId,
    DateTimeOffset CheckedAt,
    bool IsHealthy,
    int StatusCode,
    string Message);

/// <summary>Service interface for container image registry and health tracking.</summary>
public interface IContainerRegistryService
{
    /// <summary>Registers a new container image.</summary>
    Task<ContainerImage> RegisterImageAsync(RegisterImageRequest request, CancellationToken ct);

    /// <summary>Gets a container image by ID, or <see langword="null"/> if not found.</summary>
    Task<ContainerImage?> GetImageAsync(Guid imageId, CancellationToken ct);

    /// <summary>Streams all registered images.</summary>
    IAsyncEnumerable<ContainerImage> ListImagesAsync(CancellationToken ct);

    /// <summary>Records a health check result for a container image.</summary>
    Task RecordHealthCheckAsync(Guid imageId, HealthCheckResult result, CancellationToken ct);

    /// <summary>Returns the most recent health check results for an image.</summary>
    Task<IReadOnlyList<HealthCheckResult>> GetHealthHistoryAsync(Guid imageId, int limit, CancellationToken ct);

    /// <summary>Removes a container image registration.</summary>
    Task<bool> RemoveImageAsync(Guid imageId, CancellationToken ct);
}
