using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;
using MLS.Network.SubscriptionManager.Hubs;

namespace MLS.Network.SubscriptionManager.Services;

/// <summary>Thread-safe in-memory implementation of <see cref="ISubscriptionService"/>.</summary>
public sealed class SubscriptionService(
    IHubContext<SubscriptionManagerHub, ISubscriptionManagerHubClient> _hubContext,
    ILogger<SubscriptionService> _logger) : ISubscriptionService
{
    // topic → subscriptionId → info
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, SubscriptionInfo>> _byTopic = new();
    // connectionId → set of subscriptionIds
    private readonly ConcurrentDictionary<string, HashSet<string>> _byConnection = new();
    private readonly object _connectionLock = new();

    /// <inheritdoc/>
    public Task<string> SubscribeAsync(string topic, string connectionId, CancellationToken ct)
    {
        var subId   = Guid.NewGuid().ToString("N");
        var info    = new SubscriptionInfo(subId, topic, connectionId, DateTimeOffset.UtcNow);
        var topicMap = _byTopic.GetOrAdd(topic, _ => new ConcurrentDictionary<string, SubscriptionInfo>());
        topicMap[subId] = info;

        lock (_connectionLock)
        {
            if (!_byConnection.TryGetValue(connectionId, out var ids))
            {
                ids = new HashSet<string>();
                _byConnection[connectionId] = ids;
            }
            ids.Add(subId);
        }

        _logger.LogDebug("Subscribed {ConnectionId} to {Topic} as {SubId}", connectionId, topic, subId);
        return Task.FromResult(subId);
    }

    /// <inheritdoc/>
    public Task UnsubscribeAsync(string subscriptionId, CancellationToken ct)
    {
        foreach (var topicMap in _byTopic.Values)
        {
            if (topicMap.TryRemove(subscriptionId, out var info))
            {
                lock (_connectionLock)
                {
                    if (_byConnection.TryGetValue(info.ConnectionId, out var ids))
                        ids.Remove(subscriptionId);
                }
                break;
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsubscribeAllAsync(string connectionId, CancellationToken ct)
    {
        lock (_connectionLock)
        {
            if (!_byConnection.TryRemove(connectionId, out var ids)) return Task.CompletedTask;
            foreach (var subId in ids)
                foreach (var topicMap in _byTopic.Values)
                    topicMap.TryRemove(subId, out _);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<int> PublishAsync(string topic, string message, CancellationToken ct)
    {
        if (!_byTopic.TryGetValue(topic, out var topicMap)) return 0;
        var subscribers = topicMap.Values.ToList();
        foreach (var sub in subscribers)
        {
            try
            {
                var envelope = EnvelopePayload.Create(
                    MessageTypes.TopicMessage,
                    SubscriptionManagerConstants.ModuleName,
                    new { topic, message });
                await _hubContext.Clients.Client(sub.ConnectionId)
                    .ReceiveMessage(envelope)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver to {ConnectionId}", sub.ConnectionId);
            }
        }
        return subscribers.Count;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_byTopic.TryGetValue(topic, out var topicMap)) yield break;
        foreach (var info in topicMap.Values)
        {
            ct.ThrowIfCancellationRequested();
            yield return info;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetTopics() => _byTopic.Keys.ToList().AsReadOnly();
}
