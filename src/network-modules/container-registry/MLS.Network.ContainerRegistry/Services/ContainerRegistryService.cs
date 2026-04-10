using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MLS.Network.ContainerRegistry.Services;

/// <summary>Thread-safe in-memory implementation of <see cref="IContainerRegistryService"/>.</summary>
public sealed class ContainerRegistryService(ILogger<ContainerRegistryService> _logger) : IContainerRegistryService
{
    private readonly ConcurrentDictionary<Guid, ContainerImage> _images = new();
    private readonly ConcurrentDictionary<Guid, List<HealthCheckResult>> _health = new();
    private readonly object _healthLock = new();

    /// <inheritdoc/>
    public Task<ContainerImage> RegisterImageAsync(RegisterImageRequest request, CancellationToken ct)
    {
        var image = new ContainerImage(
            Guid.NewGuid(),
            request.Name,
            request.Tag,
            request.Registry,
            request.Digest,
            DateTimeOffset.UtcNow,
            null,
            false);
        _images[image.Id] = image;
        _logger.LogInformation("Registered image {Name}:{Tag} from {Registry}", request.Name, request.Tag, request.Registry);
        return Task.FromResult(image);
    }

    /// <inheritdoc/>
    public Task<ContainerImage?> GetImageAsync(Guid imageId, CancellationToken ct) =>
        Task.FromResult(_images.TryGetValue(imageId, out var image) ? image : null);

    /// <inheritdoc/>
    public async IAsyncEnumerable<ContainerImage> ListImagesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var image in _images.Values)
        {
            ct.ThrowIfCancellationRequested();
            yield return image;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public Task RecordHealthCheckAsync(Guid imageId, HealthCheckResult result, CancellationToken ct)
    {
        lock (_healthLock)
        {
            if (!_health.TryGetValue(imageId, out var list))
            {
                list = new List<HealthCheckResult>();
                _health[imageId] = list;
            }
            list.Add(result);
        }

        if (_images.TryGetValue(imageId, out var image))
        {
            _images[imageId] = image with
            {
                LastHealthAt = result.CheckedAt,
                IsHealthy    = result.IsHealthy,
            };
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<HealthCheckResult>> GetHealthHistoryAsync(
        Guid imageId, int limit, CancellationToken ct)
    {
        if (!_health.TryGetValue(imageId, out var list))
            return Task.FromResult<IReadOnlyList<HealthCheckResult>>(Array.Empty<HealthCheckResult>());

        IReadOnlyList<HealthCheckResult> result;
        lock (_healthLock)
        {
            result = list.TakeLast(limit).ToList().AsReadOnly();
        }
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<bool> RemoveImageAsync(Guid imageId, CancellationToken ct)
    {
        var removed = _images.TryRemove(imageId, out _);
        _health.TryRemove(imageId, out _);
        return Task.FromResult(removed);
    }
}
