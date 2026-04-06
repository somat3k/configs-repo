using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MLS.DataLayer.Configuration;
using MLS.DataLayer.Persistence;

namespace MLS.DataLayer.Hydra;

/// <summary>
/// Feed collector for the HYPERLIQUID perpetuals exchange.
/// Subscribes to real-time OHLCV candle updates via the HYPERLIQUID WebSocket API
/// (<c>wss://api.hyperliquid.xyz/ws</c>) using the <c>candle</c> subscription type.
/// </summary>
/// <remarks>
/// <para>
/// HYPERLIQUID WS candle subscription message format:
/// <code>
/// { "method": "subscribe", "subscription": { "type": "candle", "coin": "BTC", "interval": "1m" } }
/// </code>
/// Incoming message format:
/// <code>
/// { "channel": "candle", "data": { "t": 1714000000000, "T": 1714000059999,
///   "s": "BTC", "i": "1m", "o": "65000", "c": "65100", "h": "65200", "l": "64900",
///   "v": "12.3", "n": 150 } }
/// </code>
/// </para>
/// </remarks>
public sealed class HyperliquidFeedCollector(
    IOptions<DataLayerOptions> _options,
    ILogger<HyperliquidFeedCollector> _logger) : FeedCollector(_logger)
{
    /// <inheritdoc/>
    public override string ExchangeId => "hyperliquid";

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<CandleEntity> StreamCandlesAsync(
        FeedKey key,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var opts  = _options.Value;
        var wsUri = new Uri(opts.HyperliquidWsUrl);

        // Derive coin name: strip leading 'W' (e.g. WBTC→BTC) and normalise
        var coin     = HydraUtils.DeriveHyperliquidCoin(key.Symbol);
        var interval = HydraUtils.NormaliseHyperliquidInterval(key.Timeframe);

        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(wsUri, ct).ConfigureAwait(false);

        // Subscribe to candle stream
        var sub = JsonSerializer.SerializeToUtf8Bytes(new
        {
            method       = "subscribe",
            subscription = new { type = "candle", coin, interval }
        });

        await ws.SendAsync(sub, WebSocketMessageType.Text, endOfMessage: true, ct)
                .ConfigureAwait(false);

        var buffer      = new byte[65536];
        using var frame = new MemoryStream(65536);

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var safeSymbol = HydraUtils.SanitiseFeedId(key.Symbol);
        _logger.LogWarning(ex, "HyperliquidFeedCollector: receive error on {Symbol}", safeSymbol);
                yield break;
            }

            if (result.MessageType == WebSocketMessageType.Close) yield break;

            frame.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;

            var candle = ParseCandle(frame.GetBuffer().AsMemory(0, (int)frame.Position), key);
            frame.SetLength(0);
            frame.Position = 0;

            if (candle is not null) yield return candle;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CandleEntity? ParseCandle(ReadOnlyMemory<byte> data, FeedKey key)
    {
        try
        {
            using var doc  = JsonDocument.Parse(data);
            var root       = doc.RootElement;

            // Only handle candle channel messages
            if (!root.TryGetProperty("channel", out var channel)
                || channel.GetString() != "candle") return null;

            if (!root.TryGetProperty("data", out var d)) return null;

            // "t" is open_time in epoch milliseconds
            var openTimeMs = d.TryGetProperty("t", out var tp) ? tp.GetInt64() : 0L;
            if (openTimeMs == 0) return null;

            static double ParseD(JsonElement el, string key_)
                => el.TryGetProperty(key_, out var v)
                   && double.TryParse(v.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0.0;

            var open  = ParseD(d, "o");
            var high  = ParseD(d, "h");
            var low   = ParseD(d, "l");
            var close = ParseD(d, "c");
            var vol   = ParseD(d, "v");

            return new CandleEntity
            {
                Exchange    = key.Exchange,
                Symbol      = key.Symbol,
                Timeframe   = key.Timeframe,
                OpenTime    = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs),
                Open        = open,
                High        = high,
                Low         = low,
                Close       = close,
                Volume      = vol,
                QuoteVolume = vol * close,
                InsertedAt  = DateTimeOffset.UtcNow,
            };
        }
        catch
        {
            return null;
        }
    }
}
