using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace MLS.BlockController.Services;

/// <inheritdoc cref="ISubscriptionTable"/>
public sealed class SubscriptionTable : ISubscriptionTable
{
    // Lock-free: ConcurrentDictionary + ImmutableHashSet<Guid> via ImmutableInterlocked
    private readonly ConcurrentDictionary<string, ImmutableHashSet<Guid>> _table = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ValueTask AddAsync(string topic, Guid moduleId, CancellationToken ct = default)
    {
        _table.AddOrUpdate(
            topic,
            addValueFactory:    _   => ImmutableHashSet.Create(moduleId),
            updateValueFactory: (_, existing) => existing.Add(moduleId));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(string topic, Guid moduleId, CancellationToken ct = default)
    {
        _table.AddOrUpdate(
            topic,
            addValueFactory:    _   => ImmutableHashSet<Guid>.Empty,
            updateValueFactory: (_, existing) => existing.Remove(moduleId));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAllAsync(Guid moduleId, CancellationToken ct = default)
    {
        foreach (var key in _table.Keys)
        {
            _table.AddOrUpdate(
                key,
                _  => ImmutableHashSet<Guid>.Empty,
                (_, existing) => existing.Remove(moduleId));
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ClearStrategyAsync(Guid strategyId, CancellationToken ct = default)
    {
        var prefix = strategyId.ToString("N");
        foreach (var key in _table.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                _table.TryRemove(key, out _);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlySet<Guid> GetSubscribers(string topic) =>
        _table.TryGetValue(topic, out var set) ? set : ImmutableHashSet<Guid>.Empty;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IReadOnlySet<Guid>> GetSnapshot() =>
        _table.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<Guid>)kvp.Value,
            StringComparer.Ordinal);
}
