using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace MLS.BlockController.Services;

/// <inheritdoc cref="ISubscriptionTable"/>
public sealed class SubscriptionTable : ISubscriptionTable
{
    // Each slot holds a StrongBox wrapping an ImmutableHashSet<Guid>.
    // StrongBox provides a stable ref that ImmutableInterlocked.Update can exchange atomically
    // without taking any lock — reads on the hot path are completely wait-free.
    private readonly ConcurrentDictionary<string, StrongBox<ImmutableHashSet<Guid>>> _table =
        new(StringComparer.Ordinal);

    // Returns a ref to the set inside the box, creating the slot if missing.
    private ref ImmutableHashSet<Guid> SlotFor(string topic) =>
        ref _table.GetOrAdd(topic, _ => new StrongBox<ImmutableHashSet<Guid>>(ImmutableHashSet<Guid>.Empty)).Value!;

    /// <inheritdoc/>
    public ValueTask AddAsync(string topic, Guid moduleId, CancellationToken ct = default)
    {
        ImmutableInterlocked.Update(ref SlotFor(topic),
            static (existing, id) => existing.Add(id), moduleId);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(string topic, Guid moduleId, CancellationToken ct = default)
    {
        if (_table.TryGetValue(topic, out var box))
        {
            ImmutableInterlocked.Update(ref box.Value!, static (existing, id) => existing.Remove(id), moduleId);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAllAsync(Guid moduleId, CancellationToken ct = default)
    {
        foreach (var box in _table.Values)
        {
            ImmutableInterlocked.Update(ref box.Value!, static (existing, id) => existing.Remove(id), moduleId);
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
            {
                _table.TryRemove(key, out _);
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlySet<Guid> GetSubscribers(string topic) =>
        _table.TryGetValue(topic, out var box) ? box.Value! : ImmutableHashSet<Guid>.Empty;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IReadOnlySet<Guid>> GetSnapshot() =>
        _table.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<Guid>)(kvp.Value.Value ?? ImmutableHashSet<Guid>.Empty),
            StringComparer.Ordinal);
}
