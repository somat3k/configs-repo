using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MLS.BlockController.Hubs;
using MLS.Core.Contracts;

namespace MLS.BlockController.Services;

/// <summary>
/// In-memory implementation of <see cref="IMessageRouter"/> that routes envelopes
/// via the subscription table and broadcasts via the SignalR hub.
/// Uses a bounded <see cref="Channel{T}"/> so the hot path never blocks.
/// </summary>
public sealed class InMemoryMessageRouter : IMessageRouter, IAsyncDisposable
{
    private readonly ISubscriptionTable _subscriptions;
    private readonly IHubContext<BlockControllerHub> _hub;
    private readonly ILogger<InMemoryMessageRouter> _logger;

    private readonly Channel<EnvelopePayload> _broadcastChannel =
        Channel.CreateBounded<EnvelopePayload>(new BoundedChannelOptions(1024)
        {
            FullMode        = BoundedChannelFullMode.DropOldest,
            SingleReader    = true,
            SingleWriter    = false,
            AllowSynchronousContinuations = false,
        });

    private readonly Task _broadcastWorker;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Initialise the router and start the background broadcast worker.</summary>
    public InMemoryMessageRouter(
        ISubscriptionTable subscriptions,
        IHubContext<BlockControllerHub> hub,
        ILogger<InMemoryMessageRouter> logger)
    {
        _subscriptions = subscriptions;
        _hub           = hub;
        _logger        = logger;
        _broadcastWorker = Task.Run(BroadcastWorkerAsync);
    }

    /// <inheritdoc/>
    public Task RouteAsync(EnvelopePayload envelope, CancellationToken ct = default)
    {
        var subscribers = _subscriptions.GetSubscribers(envelope.Type);
        if (subscribers.Count == 0)
        {
            _logger.LogTrace("No subscribers for topic {Topic} — envelope dropped", envelope.Type);
            return Task.CompletedTask;
        }

        return SendToSubscribersAsync(envelope, subscribers, ct);
    }

    private async Task SendToSubscribersAsync(
        EnvelopePayload envelope,
        IReadOnlySet<Guid> subscribers,
        CancellationToken ct)
    {
        // Delivery uses SignalR Groups. Modules join a group named after their own module ID
        // when they connect to the hub (see BlockControllerHub.OnConnectedAsync, Session 04).
        // Groups do not require authentication claims, making them suitable for machine-to-machine
        // module delivery. Until Session 04 wires OnConnectedAsync, routed (non-broadcast)
        // envelopes will be silently dropped because no connection has joined the group.
        foreach (var subscriberId in subscribers)
        {
            try
            {
                await _hub.Clients
                    .Group(subscriberId.ToString())
                    .SendAsync("ReceiveEnvelope", envelope, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver envelope to group {ModuleId}", subscriberId);
            }
        }
    }

    /// <inheritdoc/>
    public Task BroadcastAsync(EnvelopePayload envelope, CancellationToken ct = default)
    {
        if (!_broadcastChannel.Writer.TryWrite(envelope))
            _logger.LogWarning("Broadcast channel full — oldest envelope dropped");

        return Task.CompletedTask;
    }

    private async Task BroadcastWorkerAsync()
    {
        await foreach (var envelope in _broadcastChannel.Reader.ReadAllAsync(_cts.Token)
            .ConfigureAwait(false))
        {
            try
            {
                await _hub.Clients.All.SendAsync("ReceiveEnvelope", envelope, _cts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broadcast error for message type {Type}", envelope.Type);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _broadcastChannel.Writer.Complete();
        await _broadcastWorker.ConfigureAwait(false);
        _cts.Dispose();
    }
}
