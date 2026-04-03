namespace MLS.BlockController.Services;

/// <summary>
/// Thread-safe, O(1) topic-to-subscriber mapping used by the message router
/// to determine which modules should receive a given envelope.
/// </summary>
/// <remarks>
/// The table uses <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
/// with <see cref="System.Collections.Immutable.ImmutableHashSet{T}"/> values so that
/// read operations on the hot path are completely lock-free.
/// </remarks>
public interface ISubscriptionTable
{
    /// <summary>Register <paramref name="moduleId"/> as a subscriber to <paramref name="topic"/>.</summary>
    public ValueTask AddAsync(string topic, Guid moduleId, CancellationToken ct = default);

    /// <summary>Remove <paramref name="moduleId"/> from <paramref name="topic"/>.</summary>
    public ValueTask RemoveAsync(string topic, Guid moduleId, CancellationToken ct = default);

    /// <summary>Remove all subscriptions for <paramref name="moduleId"/> across all topics.</summary>
    public ValueTask RemoveAllAsync(Guid moduleId, CancellationToken ct = default);

    /// <summary>
    /// Remove all subscriptions associated with <paramref name="strategyId"/>
    /// (topics prefixed with the strategy ID).
    /// </summary>
    public ValueTask ClearStrategyAsync(Guid strategyId, CancellationToken ct = default);

    /// <summary>
    /// Return the set of module IDs subscribed to <paramref name="topic"/>.
    /// Returns an empty set when no subscribers exist. Never throws.
    /// </summary>
    public IReadOnlySet<Guid> GetSubscribers(string topic);

    /// <summary>Return a snapshot of all current subscriptions (topic → subscriber IDs).</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<Guid>> GetSnapshot();
}
