using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using MLS.Broker.Configuration;
using MLS.Broker.Hubs;
using MLS.Broker.Interfaces;
using MLS.Broker.Models;
using MLS.Core.Constants;
using MLS.Core.Contracts;

namespace MLS.Broker.Services;

/// <summary>
/// Background service that subscribes to HYPERLIQUID fill and position notifications
/// via WebSocket and broadcasts them as <c>FILL_NOTIFICATION</c> and
/// <c>POSITION_UPDATE</c> envelopes to all connected SignalR clients.
/// </summary>
public sealed class FillNotificationService(
    IHyperliquidClient _hyperliquid,
    IOrderTracker _orderTracker,
    IHubContext<BrokerHub> _hub,
    IOptions<BrokerOptions> _options,
    ILogger<FillNotificationService> _logger) : BackgroundService
{
    private const string ModuleId = "broker";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts    = _options.Value;
        var channel = Channel.CreateBounded<FillNotification>(
            new BoundedChannelOptions(opts.FillChannelCapacity)
            {
                FullMode     = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = true,
            });

        // Producer: subscribe to HYPERLIQUID fill stream
        var producer = ProduceFillsAsync(channel.Writer, ct);

        // Consumer: dispatch fills as envelopes
        await foreach (var fill in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            await ProcessFillAsync(fill, ct).ConfigureAwait(false);
        }

        await producer.ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task ProduceFillsAsync(ChannelWriter<FillNotification> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var fill in _hyperliquid.SubscribeFillsAsync(ct).ConfigureAwait(false))
            {
                if (!writer.TryWrite(fill))
                    _logger.LogWarning("Fill channel full — dropping fill for {ClientOrderId}", BrokerUtils.SafeLog(fill.ClientOrderId));
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task ProcessFillAsync(FillNotification fill, CancellationToken ct)
    {
        // Fetch current order state to compute running cumulative fill + VWAP.
        var current = await _orderTracker.GetAsync(fill.ClientOrderId, ct).ConfigureAwait(false);
        if (current is null)
            _logger.LogWarning(
                "Received fill for unknown order {ClientOrderId} — order may not have been tracked",
                BrokerUtils.SafeLog(fill.ClientOrderId));

        var priorFilled = current?.FilledQuantity ?? 0m;
        var cumulativeFilled = priorFilled + fill.FillQuantity;

        // Running VWAP: weight prior fills + this fill by quantity.
        decimal? runningVwap;
        if (current?.AveragePrice is { } priorVwap && priorFilled > 0m)
            runningVwap = ((priorFilled * priorVwap) + (fill.FillQuantity * fill.FillPrice)) / cumulativeFilled;
        else
            runningVwap = fill.FillPrice;

        // State: Filled only when RemainingQuantity == 0 (explicit venue signal).
        // When RemainingQuantity is -1 (sentinel = unknown), treat as PartiallyFilled
        // until the venue sends a definitive 0.
        var newState = fill.RemainingQuantity == 0m ? OrderState.Filled : OrderState.PartiallyFilled;

        // Update order tracker with cumulative values
        await _orderTracker.UpdateAsync(
            fill.ClientOrderId, newState, cumulativeFilled, runningVwap, ct)
            .ConfigureAwait(false);

        // Build FILL_NOTIFICATION envelope
        var envelope = EnvelopePayload.Create(
            MessageTypes.FillNotification,
            ModuleId,
            fill);

        // Broadcast to all connected clients
        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveEnvelope", envelope, ct)
                  .ConfigureAwait(false);

        _logger.LogInformation(
            "Fill: {ClientOrderId} qty={FillQty} @ {FillPrice} cumulative={Cumulative} state={State}",
            fill.ClientOrderId, fill.FillQuantity, fill.FillPrice, cumulativeFilled, newState);
    }
}
