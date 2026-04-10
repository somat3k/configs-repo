using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.DeFi.Configuration;
using MLS.DeFi.Hubs;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;

namespace MLS.DeFi.Services;

/// <summary>
/// Background service that subscribes to HYPERLIQUID fill and position updates via
/// WebSocket and broadcasts them as <c>FILL_NOTIFICATION</c> and <c>POSITION_UPDATE</c>
/// envelopes to all connected SignalR clients.
/// </summary>
public sealed class PositionMonitorService(
    IHyperliquidClient _hyperliquid,
    IHubContext<DeFiHub> _hub,
    IOptions<DeFiOptions> _options,
    ILogger<PositionMonitorService> _logger) : BackgroundService
{
    private const string ModuleId = "defi";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts    = _options.Value;
        var channel = Channel.CreateBounded<DeFiFillNotification>(
            new BoundedChannelOptions(opts.PositionChannelCapacity)
            {
                FullMode     = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = true,
            });

        var producer = ProduceFillsAsync(channel.Writer, ct);

        await foreach (var fill in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            await BroadcastFillAsync(fill, ct).ConfigureAwait(false);
        }

        await producer.ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task ProduceFillsAsync(ChannelWriter<DeFiFillNotification> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var fill in _hyperliquid.SubscribeFillsAsync(ct).ConfigureAwait(false))
            {
                if (!writer.TryWrite(fill))
                    _logger.LogWarning(
                        "Fill channel full — dropping fill for {ClientOrderId}",
                        DeFiUtils.SafeLog(fill.ClientOrderId));
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

    private async Task BroadcastFillAsync(DeFiFillNotification fill, CancellationToken ct)
    {
        var envelope = EnvelopePayload.Create(MessageTypes.FillNotification, ModuleId, fill);

        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveEnvelope", envelope, ct)
                  .ConfigureAwait(false);

        _logger.LogInformation(
            "FILL_NOTIFICATION broadcast: {ClientOrderId} qty={FillQty} price={FillPrice}",
            DeFiUtils.SafeLog(fill.ClientOrderId), fill.FillQuantity, fill.FillPrice);
    }
}
