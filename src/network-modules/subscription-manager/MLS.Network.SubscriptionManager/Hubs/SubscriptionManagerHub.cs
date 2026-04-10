using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;

namespace MLS.Network.SubscriptionManager.Hubs;

/// <summary>SignalR hub exposing the subscription-manager WebSocket API on port 6012.</summary>
public sealed class SubscriptionManagerHub(
    ISubscriptionService _service,
    ILogger<SubscriptionManagerHub> _logger) : Hub<ISubscriptionManagerHubClient>
{
    /// <summary>Subscribes the caller to a topic.</summary>
    public async Task Subscribe(EnvelopePayload envelope)
    {
        var topic = envelope.Payload.TryGetProperty("topic", out var t)
            ? t.GetString() ?? string.Empty : string.Empty;
        var subId = await _service.SubscribeAsync(topic, Context.ConnectionId, Context.ConnectionAborted)
            .ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.TopicSubscribed,
            SubscriptionManagerConstants.ModuleName,
            new { subscription_id = subId, topic });
        await Clients.Caller.ReceiveSubscriptionAck(response).ConfigureAwait(false);
    }

    /// <summary>Unsubscribes from a specific subscription by ID.</summary>
    public async Task Unsubscribe(EnvelopePayload envelope)
    {
        var subId = envelope.Payload.TryGetProperty("subscription_id", out var s)
            ? s.GetString() ?? string.Empty : string.Empty;
        await _service.UnsubscribeAsync(subId, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.TopicUnsubscribed,
            SubscriptionManagerConstants.ModuleName,
            new { subscription_id = subId });
        await Clients.Caller.ReceiveSubscriptionAck(response).ConfigureAwait(false);
    }

    /// <summary>Publishes a message to a topic.</summary>
    public async Task Publish(EnvelopePayload envelope)
    {
        var topic   = envelope.Payload.TryGetProperty("topic", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        var message = envelope.Payload.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;
        var count   = await _service.PublishAsync(topic, message, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.TopicMessage,
            SubscriptionManagerConstants.ModuleName,
            new { topic, delivered_to = count });
        await Clients.Caller.ReceiveMessage(response).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        await _service.UnsubscribeAllAsync(Context.ConnectionId, CancellationToken.None)
            .ConfigureAwait(false);
        await base.OnDisconnectedAsync(ex).ConfigureAwait(false);
    }
}

/// <summary>Client-side methods pushed by the SubscriptionManager hub.</summary>
public interface ISubscriptionManagerHubClient
{
    /// <summary>Receives a published topic message.</summary>
    Task ReceiveMessage(EnvelopePayload envelope);

    /// <summary>Receives a subscription acknowledgement.</summary>
    Task ReceiveSubscriptionAck(EnvelopePayload envelope);
}
