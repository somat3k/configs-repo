using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MLS.Network.UniqueIdGenerator.Services;

/// <summary>Thread-safe implementation of <see cref="IUniqueIdService"/>.</summary>
public sealed class UniqueIdService : IUniqueIdService
{
    private readonly ConcurrentDictionary<string, long> _counters = new();

    /// <inheritdoc/>
    public string GenerateUuid() => Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public long GenerateSequentialId(string prefix) =>
        _counters.AddOrUpdate(prefix, 1L, (_, prev) => prev + 1);

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamUuidsAsync(
        int count,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return GenerateUuid();
            await Task.Yield();
        }
    }
}
