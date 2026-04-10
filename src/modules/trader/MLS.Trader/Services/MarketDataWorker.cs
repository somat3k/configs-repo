using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Trader;
using MLS.Trader.Configuration;
using MLS.Trader.Interfaces;
using MLS.Trader.Models;

namespace MLS.Trader.Services;

/// <summary>
/// Background service that subscribes to the Block Controller SignalR hub and processes
/// <c>MARKET_DATA_UPDATE</c>, <c>INFERENCE_RESULT</c>, and <c>POSITION_UPDATE</c> envelopes.
/// For each actionable market snapshot the service generates a trade signal, computes risk
/// parameters, creates an order via <see cref="IOrderManager"/>, and broadcasts a
/// <c>TRADE_SIGNAL</c> envelope.
/// </summary>
public sealed class MarketDataWorker(
    ISignalEngine _signalEngine,
    IRiskManager _riskManager,
    IOrderManager _orderManager,
    IEnvelopeSender _sender,
    ModuleIdentity _identity,
    IOptions<TraderOptions> _options,
    ILogger<MarketDataWorker> _logger) : BackgroundService
{
    private const string ModuleId = "trader";

    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
    ];

    // In-memory position cache updated on POSITION_UPDATE envelopes.
    private readonly ConcurrentDictionary<string, TraderPosition> _positions = new();

    /// <summary>Read-only snapshot of currently tracked positions.</summary>
    internal IReadOnlyDictionary<string, TraderPosition> Positions => _positions;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        var channelOpts = new BoundedChannelOptions(opts.SignalChannelCapacity)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        };
        var channel = Channel.CreateBounded<EnvelopePayload>(channelOpts);

        // Start the consumer task that processes envelopes from the channel.
        var consumer = ConsumeEnvelopesAsync(channel.Reader, ct);

        // Connect to Block Controller hub and pipe incoming envelopes into the channel.
        await RunHubConnectionAsync(channel.Writer, ct).ConfigureAwait(false);

        channel.Writer.TryComplete();
        await consumer.ConfigureAwait(false);
    }

    // ── Hub connection loop ───────────────────────────────────────────────────────

    private async Task RunHubConnectionAsync(ChannelWriter<EnvelopePayload> writer, CancellationToken ct)
    {
        var opts   = _options.Value;

        while (!ct.IsCancellationRequested)
        {
            var hubUrl = $"{opts.BlockControllerUrl}/hubs/block-controller?moduleId={_identity.Id}";

            var hub = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(ReconnectDelays)
                .Build();

            hub.On<EnvelopePayload>("ReceiveEnvelope", envelope =>
            {
                if (!writer.TryWrite(envelope))
                    _logger.LogWarning("MarketDataWorker: channel full — dropping envelope type={Type}", envelope.Type);
            });

            hub.Closed += ex =>
            {
                if (ex is not null)
                    _logger.LogWarning(ex, "MarketDataWorker: hub connection closed unexpectedly");
                return Task.CompletedTask;
            };

            try
            {
                await hub.StartAsync(ct).ConfigureAwait(false);

                await hub.InvokeAsync("SubscribeToTopicAsync", MessageTypes.MarketDataUpdate, ct).ConfigureAwait(false);
                await hub.InvokeAsync("SubscribeToTopicAsync", MessageTypes.InferenceResult, ct).ConfigureAwait(false);
                await hub.InvokeAsync("SubscribeToTopicAsync", MessageTypes.PositionUpdate, ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "MarketDataWorker: connected to Block Controller hub as moduleId={Id}", _identity.Id);

                await WaitUntilCancelledAsync(hub, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MarketDataWorker: hub connection failed — retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            finally
            {
                await hub.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task WaitUntilCancelledAsync(HubConnection hub, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetResult());
        hub.Closed += _ => { tcs.TrySetResult(); return Task.CompletedTask; };
        await tcs.Task.ConfigureAwait(false);
        await hub.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    // ── Envelope consumer ─────────────────────────────────────────────────────────

    private async Task ConsumeEnvelopesAsync(ChannelReader<EnvelopePayload> reader, CancellationToken ct)
    {
        await foreach (var envelope in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                await HandleEnvelopeAsync(envelope, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "MarketDataWorker: error handling envelope type={Type}", envelope.Type);
            }
        }
    }

    private async Task HandleEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct)
    {
        switch (envelope.Type)
        {
            case MessageTypes.MarketDataUpdate:
                await HandleMarketDataAsync(envelope, ct).ConfigureAwait(false);
                break;

            case MessageTypes.PositionUpdate:
                HandlePositionUpdate(envelope);
                break;

            case MessageTypes.InferenceResult:
                // INFERENCE_RESULT from ml-runtime may contain a pre-computed signal payload;
                // log receipt — the trader uses its own local model-t by default.
                _logger.LogDebug("MarketDataWorker: received INFERENCE_RESULT from module={Module}",
                    envelope.ModuleId);
                break;
        }
    }

    private async Task HandleMarketDataAsync(EnvelopePayload envelope, CancellationToken ct)
    {
        MarketDataPayload? payload;
        try
        {
            payload = envelope.Payload.Deserialize<MarketDataPayload>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "MarketDataWorker: failed to deserialize MARKET_DATA_UPDATE payload");
            return;
        }

        if (payload is null) return;

        var features = new MarketFeatures(
            Symbol:          payload.Symbol,
            Price:           payload.Price,
            Rsi:             payload.Rsi,
            MacdValue:       payload.MacdValue,
            MacdSignal:      payload.MacdSignal,
            BollingerUpper:  payload.BollingerUpper,
            BollingerMiddle: payload.BollingerMiddle,
            BollingerLower:  payload.BollingerLower,
            VolumeDelta:     payload.VolumeDelta,
            Momentum:        payload.Momentum,
            AtrValue:        payload.AtrValue,
            Timestamp:       payload.Timestamp);

        var signal = await _signalEngine.GenerateSignalAsync(features, ct).ConfigureAwait(false);

        _logger.LogDebug(
            "MarketDataWorker: signal={Direction} confidence={Conf:F3} symbol={Symbol}",
            signal.Direction, signal.Confidence, signal.Symbol);

        var opts = _options.Value;

        // Only act on actionable signals above the confidence threshold
        if (signal.Direction == SignalDirection.Hold || signal.Confidence < opts.MinSignalConfidence)
            return;

        // Compute risk parameters
        var positionSizeUsd = _riskManager.ComputePositionSize(signal.Confidence, opts.RiskRewardRatio);
        if (positionSizeUsd <= 0m) return;

        var stopLoss   = _riskManager.ComputeStopLoss(payload.Price, signal.Direction, payload.AtrValue);
        var takeProfit = _riskManager.ComputeTakeProfit(payload.Price, stopLoss, signal.Direction);

        // Compute order quantity in base asset units
        var quantity = payload.Price > 0m ? Math.Round(positionSizeUsd / payload.Price, 8) : 0m;
        if (quantity <= 0m) return;

        // Create order
        var order = await _orderManager.CreateOrderAsync(
            symbol:          payload.Symbol,
            direction:       signal.Direction,
            quantity:        quantity,
            entryPrice:      payload.Price,
            stopLossPrice:   stopLoss,
            takeProfitPrice: takeProfit,
            paperTrading:    opts.PaperTrading,
            ct:              ct).ConfigureAwait(false);

        // Broadcast TRADE_SIGNAL envelope
        var signalPayload = new TradeSignalPayload(
            Symbol:          payload.Symbol,
            Direction:       signal.Direction.ToString(),
            Confidence:      signal.Confidence,
            PositionSizeUsd: positionSizeUsd,
            EntryPrice:      payload.Price,
            StopLossPrice:   stopLoss,
            TakeProfitPrice: takeProfit,
            PaperTrading:    opts.PaperTrading,
            ClientOrderId:   order.ClientOrderId,
            Timestamp:       signal.Timestamp);

        var signalEnvelope = EnvelopePayload.Create(MessageTypes.TradeSignal, ModuleId, signalPayload);
        await _sender.SendEnvelopeAsync(signalEnvelope, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "MarketDataWorker: TRADE_SIGNAL {Direction} {Symbol} qty={Qty} size={Size:F2} USD conf={Conf:F3}",
            signal.Direction, payload.Symbol, quantity, positionSizeUsd, signal.Confidence);
    }

    private void HandlePositionUpdate(EnvelopePayload envelope)
    {
        try
        {
            using var doc    = JsonDocument.Parse(envelope.Payload.GetRawText());
            var root         = doc.RootElement;
            var symbol       = root.GetProperty("symbol").GetString() ?? string.Empty;
            var directionStr = root.TryGetProperty("side", out var sideEl) ? sideEl.GetString() : "Buy";
            var direction    = directionStr?.Equals("Sell", StringComparison.OrdinalIgnoreCase) == true
                               ? SignalDirection.Sell : SignalDirection.Buy;
            var qty           = root.TryGetProperty("quantity", out var qEl) ? qEl.GetDecimal() : 0m;
            var avgEntry      = root.TryGetProperty("average_entry_price", out var aeEl) ? aeEl.GetDecimal() : 0m;
            var pnl           = root.TryGetProperty("unrealised_pnl", out var pnlEl) ? pnlEl.GetDecimal() : 0m;
            var venue         = root.TryGetProperty("venue", out var vEl) ? vEl.GetString() ?? "hyperliquid" : "hyperliquid";

            _positions[symbol] = new TraderPosition(
                Symbol:            symbol,
                Direction:         direction,
                Quantity:          qty,
                AverageEntryPrice: avgEntry,
                UnrealisedPnl:     pnl,
                Venue:             venue,
                UpdatedAt:         DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MarketDataWorker: failed to parse POSITION_UPDATE payload");
        }
    }
}
