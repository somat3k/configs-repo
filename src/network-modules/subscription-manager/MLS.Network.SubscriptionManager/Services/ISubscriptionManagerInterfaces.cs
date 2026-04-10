using System.Runtime.CompilerServices;

namespace MLS.Network.SubscriptionManager.Services;

/// <summary>Represents a single subscription entry.</summary>
public sealed record SubscriptionInfo(
    string SubscriptionId,
    string Topic,
    string ConnectionId,
    DateTimeOffset CreatedAt);

/// <summary>Service interface for topic-based pub/sub operations.</summary>
public interface ISubscriptionService
{
    /// <summary>Subscribes a connection to a topic and returns the new subscription ID.</summary>
    Task<string> SubscribeAsync(string topic, string connectionId, CancellationToken ct);

    /// <summary>Removes a single subscription by its ID.</summary>
    Task UnsubscribeAsync(string subscriptionId, CancellationToken ct);

    /// <summary>Removes all subscriptions for a given connection.</summary>
    Task UnsubscribeAllAsync(string connectionId, CancellationToken ct);

    /// <summary>Publishes a message to all subscribers of a topic. Returns the subscriber count.</summary>
    Task<int> PublishAsync(string topic, string message, CancellationToken ct);

    /// <summary>Streams all subscriptions for a given topic.</summary>
    IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topic, CancellationToken ct);

    /// <summary>Returns all known topic names.</summary>
    IReadOnlyList<string> GetTopics();
}
