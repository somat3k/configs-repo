using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Broker.Configuration;
using MLS.Broker.Interfaces;
using MLS.Broker.Models;

namespace MLS.Broker.Services;

/// <summary>
/// HYPERLIQUID REST + WebSocket client.
/// <para>
/// REST API endpoint: <c>https://api.hyperliquid.xyz/info</c> (public),
/// <c>https://api.hyperliquid.xyz/exchange</c> (order placement — requires signature).
/// </para>
/// <para>
/// WebSocket: <c>wss://api.hyperliquid.xyz/ws</c> — fill and order book subscriptions.
/// </para>
/// </summary>
/// <remarks>
/// HYPERLIQUID uses a JSON-over-HTTP REST API and a JSON WebSocket protocol.
/// Order placement requires an EVM-compatible signature; in production the private key
/// is injected via environment variables and never stored in config files.
/// </remarks>
public sealed class HyperliquidClient(
    HttpClient _http,
    IOptions<BrokerOptions> _options,
    ILogger<HyperliquidClient> _logger) : IHyperliquidClient
{
    private const string VenueId = "hyperliquid";

    /// <inheritdoc/>
    public async Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        // HYPERLIQUID order placement payload (simplified — production adds EVM signature)
        var orderPayload = new
        {
            action = new
            {
                type   = "order",
                orders = new[]
                {
                    new
                    {
                        a    = 0,             // asset index (resolved from symbol at runtime)
                        b    = request.Side == OrderSide.Buy,
                        p    = request.LimitPrice?.ToString("F8") ?? "0",
                        s    = request.Quantity.ToString("F8"),
                        r    = false,         // reduceOnly
                        t    = BuildOrderTypePayload(request),
                        c    = request.ClientOrderId,
                    }
                },
                grouping = "na",
            },
            nonce     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            signature = new { r = "0x0", s = "0x0", v = 28 }, // Placeholder — replace with real signer
        };

        try
        {
            var response = await _http.PostAsJsonAsync("/exchange", orderPayload, ct)
                                       .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct)
                .ConfigureAwait(false);

            var status    = doc.RootElement.GetProperty("status").GetString();
            var venueId   = TryGetVenueOrderId(doc.RootElement);
            var orderState = status == "ok" ? OrderState.Open : OrderState.Rejected;

            return CreateOrderResult(request, venueId, orderState);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID PlaceOrder failed for {ClientOrderId}", request.ClientOrderId);
            return CreateOrderResult(request, null, OrderState.Rejected);
        }
    }

    /// <inheritdoc/>
    public async Task<OrderResult> CancelOrderAsync(string clientOrderId, CancellationToken ct)
    {
        var payload = new
        {
            action = new
            {
                type    = "cancel",
                cancels = new[] { new { a = 0, o = clientOrderId } },
            },
            nonce     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            signature = new { r = "0x0", s = "0x0", v = 28 },
        };

        try
        {
            var response = await _http.PostAsJsonAsync("/exchange", payload, ct)
                                       .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return new OrderResult(clientOrderId, null, OrderState.Cancelled,
                0m, null, VenueId, string.Empty, OrderSide.Buy,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID CancelOrder failed for {ClientOrderId}", clientOrderId);
            return new OrderResult(clientOrderId, null, OrderState.Rejected,
                0m, null, VenueId, string.Empty, OrderSide.Buy,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrderResult>> GetOpenOrdersAsync(string symbol, CancellationToken ct)
    {
        var payload = new { type = "openOrders", user = GetWalletAddress() };
        try
        {
            var response = await _http.PostAsJsonAsync("/info", payload, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct)
                .ConfigureAwait(false);

            var results = new List<OrderResult>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var coin = item.TryGetProperty("coin", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrEmpty(symbol) && !coin.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                    continue;

                var oid      = item.TryGetProperty("oid",      out var o) ? o.GetInt64().ToString() : string.Empty;
                var side_str = item.TryGetProperty("side",     out var s) ? s.GetString() : "B";
                var side     = side_str == "A" ? OrderSide.Sell : OrderSide.Buy;
                var qty      = item.TryGetProperty("sz",       out var q) ? ParseDecimal(q) : 0m;
                var price    = item.TryGetProperty("limitPx",  out var p) ? ParseDecimal(p) : (decimal?)null;
                var cloid    = item.TryGetProperty("cloid",    out var cl) ? cl.GetString() ?? oid : oid;

                results.Add(new OrderResult(cloid, oid, OrderState.Open, 0m, price, VenueId, coin, side,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID GetOpenOrders failed for symbol={Symbol}", symbol);
            return Array.Empty<OrderResult>();
        }
    }

    /// <inheritdoc/>
    public async Task<PositionSnapshot?> GetPositionAsync(string symbol, CancellationToken ct)
    {
        var payload = new { type = "clearinghouseState", user = GetWalletAddress() };
        try
        {
            var response = await _http.PostAsJsonAsync("/info", payload, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct)
                .ConfigureAwait(false);

            if (!doc.RootElement.TryGetProperty("assetPositions", out var assetPositions))
                return null;

            foreach (var ap in assetPositions.EnumerateArray())
            {
                if (!ap.TryGetProperty("position", out var pos)) continue;

                var coin = pos.TryGetProperty("coin", out var c) ? c.GetString() : string.Empty;
                if (!string.Equals(coin, symbol.Split('-')[0], StringComparison.OrdinalIgnoreCase)) continue;

                var szi        = ParseDecimal(pos.TryGetProperty("szi", out var s) ? s : default);
                var entryPx    = ParseDecimal(pos.TryGetProperty("entryPx", out var e) ? e : default);
                var unrealPnl  = ParseDecimal(pos.TryGetProperty("unrealizedPnl", out var u) ? u : default);
                var side       = szi >= 0 ? OrderSide.Buy : OrderSide.Sell;

                return new PositionSnapshot(symbol, side, Math.Abs(szi), entryPx, unrealPnl,
                    VenueId, DateTimeOffset.UtcNow);
            }

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID GetPosition failed for symbol={Symbol}", symbol);
            return null;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FillNotification> SubscribeFillsAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var opts  = _options.Value;
        var wsUri = new Uri(opts.HyperliquidWsUrl);

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();

            bool connected;
            try
            {
                await ws.ConnectAsync(wsUri, ct).ConfigureAwait(false);
                connected = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "HYPERLIQUID fill WS connect failed — retrying in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                continue;
            }

            if (!connected) continue;

            // Subscribe to user fills
            var sub = JsonSerializer.SerializeToUtf8Bytes(new
            {
                method       = "subscribe",
                subscription = new { type = "userFills", user = GetWalletAddress() }
            });

            await ws.SendAsync(sub, WebSocketMessageType.Text, endOfMessage: true, ct)
                    .ConfigureAwait(false);

            var buffer = new byte[65536];
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
                    _logger.LogWarning(ex, "HYPERLIQUID fill WS receive error");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close) break;

                frame.Write(buffer, 0, result.Count);

                if (!result.EndOfMessage) continue;

                frame.Position = 0;
                var fill = TryParseFill(frame);
                frame.SetLength(0);

                if (fill is not null)
                    yield return fill;
            }

            if (!ct.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<OrderBookUpdate> SubscribeOrderBookAsync(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var opts  = _options.Value;
        var wsUri = new Uri(opts.HyperliquidWsUrl);
        var coin  = symbol.Split('-')[0].ToUpperInvariant();

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();

            try
            {
                await ws.ConnectAsync(wsUri, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "HYPERLIQUID orderbook WS connect failed — retrying in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                continue;
            }

            var sub = JsonSerializer.SerializeToUtf8Bytes(new
            {
                method       = "subscribe",
                subscription = new { type = "l2Book", coin }
            });

            await ws.SendAsync(sub, WebSocketMessageType.Text, endOfMessage: true, ct)
                    .ConfigureAwait(false);

            var buffer = new byte[65536];
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
                    _logger.LogWarning(ex, "HYPERLIQUID orderbook WS receive error for {Symbol}", coin);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close) break;

                frame.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage) continue;

                frame.Position = 0;
                var update = TryParseOrderBook(symbol, frame);
                frame.SetLength(0);

                if (update is not null)
                    yield return update;
            }

            if (!ct.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static object BuildOrderTypePayload(PlaceOrderRequest req) => req.Type switch
    {
        Models.OrderType.Market     => new { limit = new { tif = "Ioc" } },
        Models.OrderType.Limit      => new { limit = new { tif = "Gtc" } },
        Models.OrderType.StopMarket => new { trigger = new { isMarket = true,  tpsl = "sl", triggerPx = req.StopPrice?.ToString("F8") ?? "0" } },
        Models.OrderType.StopLimit  => new { trigger = new { isMarket = false, tpsl = "sl", triggerPx = req.StopPrice?.ToString("F8") ?? "0" } },
        _                           => new { limit = new { tif = "Gtc" } }
    };

    private static OrderResult CreateOrderResult(PlaceOrderRequest req, string? venueId, OrderState state)
        => new(req.ClientOrderId, venueId, state, 0m, null, VenueId,
               req.Symbol, req.Side, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static string? TryGetVenueOrderId(JsonElement root)
    {
        try
        {
            return root.GetProperty("response")
                       .GetProperty("data")
                       .GetProperty("statuses")[0]
                       .GetProperty("resting")
                       .GetProperty("oid")
                       .GetInt64()
                       .ToString();
        }
        catch
        {
            return null;
        }
    }

    private FillNotification? TryParseFill(Stream json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("channel", out var ch) || ch.GetString() != "userFills")
                return null;

            if (!root.TryGetProperty("data", out var data)) return null;

            // data is an array of fill objects
            var fills = data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray()
                : (data.TryGetProperty("fills", out var f) ? f.EnumerateArray() : default);

            foreach (var fill in fills)
            {
                var coin   = fill.TryGetProperty("coin",   out var c) ? c.GetString() ?? string.Empty : string.Empty;
                var side_s = fill.TryGetProperty("side",   out var s) ? s.GetString() : "B";
                var side   = side_s == "A" ? OrderSide.Sell : OrderSide.Buy;
                var sz     = ParseDecimal(fill.TryGetProperty("sz",   out var q) ? q : default);
                var px     = ParseDecimal(fill.TryGetProperty("px",   out var p) ? p : default);
                var cloid  = fill.TryGetProperty("cloid", out var cl) ? cl.GetString() ?? string.Empty : string.Empty;
                var oid    = fill.TryGetProperty("oid",   out var o)  ? o.GetInt64().ToString() : string.Empty;

                return new FillNotification(cloid, oid, coin, side, sz, px, sz, 0m, VenueId, DateTimeOffset.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse fill message");
        }

        return null;
    }

    private OrderBookUpdate? TryParseOrderBook(string symbol, Stream json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("channel", out var ch) || ch.GetString() != "l2Book")
                return null;

            if (!root.TryGetProperty("data", out var data)) return null;

            var bids = ParseLevels(data.TryGetProperty("levels", out var lv) ? lv[0] : default);
            var asks = ParseLevels(data.TryGetProperty("levels", out var lv2) ? lv2[1] : default);

            return new OrderBookUpdate(symbol, bids, asks, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse order book message");
            return null;
        }
    }

    private static IReadOnlyList<(decimal, decimal)> ParseLevels(JsonElement arr)
    {
        if (arr.ValueKind != JsonValueKind.Array) return Array.Empty<(decimal, decimal)>();

        var levels = new List<(decimal, decimal)>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            var px = ParseDecimal(item.TryGetProperty("px", out var p) ? p : default);
            var sz = ParseDecimal(item.TryGetProperty("sz", out var s) ? s : default);
            levels.Add((px, sz));
        }

        return levels;
    }

    private static decimal ParseDecimal(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Number => el.GetDecimal(),
        JsonValueKind.String when decimal.TryParse(el.GetString(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var d) => d,
        _ => 0m
    };

    /// <summary>
    /// Returns the wallet address for the module account.
    /// Loaded from the <c>HYPERLIQUID_WALLET_ADDRESS</c> environment variable.
    /// </summary>
    private static string GetWalletAddress()
        => Environment.GetEnvironmentVariable("HYPERLIQUID_WALLET_ADDRESS") ?? string.Empty;
}
