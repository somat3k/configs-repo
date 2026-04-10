using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MLS.DeFi.Configuration;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;

namespace MLS.DeFi.Services;

/// <summary>
/// HYPERLIQUID REST + WebSocket client for the DeFi module.
/// <para>
/// REST base URL: <c>https://api.hyperliquid.xyz</c><br/>
/// WebSocket URL: <c>wss://api.hyperliquid.xyz/ws</c>
/// </para>
/// <para>
/// Order placement requires an EVM-compatible signature; in production the private key
/// is managed exclusively through <c>IWalletProvider</c> — never stored in config.
/// </para>
/// </summary>
public sealed class HyperliquidClient(
    HttpClient _http,
    IOptions<DeFiOptions> _options,
    ILogger<HyperliquidClient> _logger) : IHyperliquidClient
{
    private const string VenueId = "hyperliquid";

    /// <inheritdoc/>
    public async Task<DeFiOrderResult> PlaceOrderAsync(DeFiOrderRequest request, CancellationToken ct)
    {
        var orderPayload = new
        {
            action = new
            {
                type   = "order",
                orders = new[]
                {
                    new
                    {
                        a = 0,
                        b = request.Side == DeFiOrderSide.Buy,
                        p = request.LimitPrice?.ToString("F8") ?? "0",
                        s = request.Quantity.ToString("F8"),
                        r = false,
                        t = BuildOrderTypePayload(request),
                        c = request.ClientOrderId,
                    }
                },
                grouping = "na",
            },
            nonce     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            signature = new { r = "0x0", s = "0x0", v = 28 },
        };

        try
        {
            var response = await _http.PostAsJsonAsync("/exchange", orderPayload, ct)
                                       .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            var status    = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            var venueId   = TryGetVenueOrderId(doc.RootElement);
            var state     = status == "ok" ? DeFiOrderState.Open : DeFiOrderState.Rejected;

            return CreateResult(request, venueId, state);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID PlaceOrder failed for {ClientOrderId}",
                DeFiUtils.SafeLog(request.ClientOrderId));
            return CreateResult(request, null, DeFiOrderState.Rejected);
        }
    }

    /// <inheritdoc/>
    public async Task<DeFiOrderResult> CancelOrderAsync(string clientOrderId, CancellationToken ct)
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
            var response = await _http.PostAsJsonAsync("/exchange", payload, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return new DeFiOrderResult(clientOrderId, null, DeFiOrderState.Cancelled,
                0m, null, VenueId, string.Empty, DeFiOrderSide.Buy,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID CancelOrder failed for {ClientOrderId}",
                DeFiUtils.SafeLog(clientOrderId));
            return new DeFiOrderResult(clientOrderId, null, DeFiOrderState.Rejected,
                0m, null, VenueId, string.Empty, DeFiOrderSide.Buy,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeFiOrderResult>> GetOpenOrdersAsync(string symbol, CancellationToken ct)
    {
        var payload = new { type = "openOrders", user = GetWalletAddress() };
        try
        {
            var response = await _http.PostAsJsonAsync("/info", payload, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            var results = new List<DeFiOrderResult>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var coin   = item.TryGetProperty("coin",    out var c) ? c.GetString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrEmpty(symbol) &&
                    !coin.Equals(symbol, StringComparison.OrdinalIgnoreCase)) continue;

                var oid    = item.TryGetProperty("oid",     out var o) ? o.GetInt64().ToString() : string.Empty;
                var sideS  = item.TryGetProperty("side",    out var sd) ? sd.GetString() : "B";
                var side   = sideS == "A" ? DeFiOrderSide.Sell : DeFiOrderSide.Buy;
                var qty    = ParseDecimal(item.TryGetProperty("sz",      out var q) ? q : default);
                var price  = ParseDecimal(item.TryGetProperty("limitPx", out var p) ? p : default);
                var cloid  = item.TryGetProperty("cloid",  out var cl) ? cl.GetString() ?? oid : oid;

                results.Add(new DeFiOrderResult(cloid, oid, DeFiOrderState.Open, 0m, price,
                    VenueId, coin, side, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID GetOpenOrders failed for symbol={Symbol}", symbol);
            return Array.Empty<DeFiOrderResult>();
        }
    }

    /// <inheritdoc/>
    public async Task<DeFiPositionSnapshot?> GetPositionAsync(string symbol, CancellationToken ct)
    {
        var payload = new { type = "clearinghouseState", user = GetWalletAddress() };
        try
        {
            var response = await _http.PostAsJsonAsync("/info", payload, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            if (!doc.RootElement.TryGetProperty("assetPositions", out var assetPositions))
                return null;

            var baseCoin = symbol.Split('-')[0];
            foreach (var ap in assetPositions.EnumerateArray())
            {
                if (!ap.TryGetProperty("position", out var pos)) continue;

                var coin = pos.TryGetProperty("coin", out var c) ? c.GetString() : string.Empty;
                if (!string.Equals(coin, baseCoin, StringComparison.OrdinalIgnoreCase)) continue;

                var szi       = ParseDecimal(pos.TryGetProperty("szi",          out var sz) ? sz : default);
                var entryPx   = ParseDecimal(pos.TryGetProperty("entryPx",      out var ep) ? ep : default);
                var unrealPnl = ParseDecimal(pos.TryGetProperty("unrealizedPnl", out var up) ? up : default);
                var side      = szi >= 0 ? DeFiOrderSide.Buy : DeFiOrderSide.Sell;

                return new DeFiPositionSnapshot(symbol, side, Math.Abs(szi), entryPx, unrealPnl,
                    VenueId, DateTimeOffset.UtcNow);
            }

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID GetPosition failed for symbol={Symbol}",
                DeFiUtils.SafeLog(symbol));
            return null;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DeFiFillNotification> SubscribeFillsAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var wsUri = new Uri(_options.Value.HyperliquidWsUrl);

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();

            try
            {
                await ws.ConnectAsync(wsUri, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "HYPERLIQUID fill WS connect failed — retrying in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                continue;
            }

            var sub = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
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
    public async IAsyncEnumerable<DeFiOrderBookUpdate> SubscribeOrderBookAsync(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var wsUri = new Uri(_options.Value.HyperliquidWsUrl);
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
                _logger.LogWarning(ex, "HYPERLIQUID order book WS connect failed — retrying in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                continue;
            }

            var sub = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
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
                    _logger.LogWarning(ex, "HYPERLIQUID order book WS receive error for {Coin}", coin);
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

    private static object BuildOrderTypePayload(DeFiOrderRequest req) => req.Type switch
    {
        DeFiOrderType.Market     => new { limit = new { tif = "Ioc" } },
        DeFiOrderType.Limit      => new { limit = new { tif = "Gtc" } },
        DeFiOrderType.StopMarket => new { trigger = new { isMarket = true,  tpsl = "sl", triggerPx = req.StopPrice?.ToString("F8") ?? "0" } },
        DeFiOrderType.StopLimit  => new { trigger = new { isMarket = false, tpsl = "sl", triggerPx = req.StopPrice?.ToString("F8") ?? "0" } },
        _                        => new { limit = new { tif = "Gtc" } }
    };

    private static DeFiOrderResult CreateResult(DeFiOrderRequest req, string? venueId, DeFiOrderState state)
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

    private DeFiFillNotification? TryParseFill(Stream json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("channel", out var ch) || ch.GetString() != "userFills")
                return null;

            if (!root.TryGetProperty("data", out var data)) return null;

            var fills = data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray()
                : (data.TryGetProperty("fills", out var f) ? f.EnumerateArray() : default);

            foreach (var fill in fills)
            {
                var coin  = fill.TryGetProperty("coin",  out var c)  ? c.GetString()  ?? string.Empty : string.Empty;
                var sideS = fill.TryGetProperty("side",  out var sd) ? sd.GetString() : "B";
                var side  = sideS == "A" ? DeFiOrderSide.Sell : DeFiOrderSide.Buy;
                var sz    = ParseDecimal(fill.TryGetProperty("sz",   out var q)  ? q  : default);
                var px    = ParseDecimal(fill.TryGetProperty("px",   out var p)  ? p  : default);
                var cloid = fill.TryGetProperty("cloid", out var cl) ? cl.GetString() ?? string.Empty : string.Empty;
                var oid   = fill.TryGetProperty("oid",   out var o)  ? o.GetInt64().ToString() : string.Empty;

                return new DeFiFillNotification(cloid, oid, coin, side, sz, px, sz, -1m,
                    VenueId, DateTimeOffset.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse fill message");
        }

        return null;
    }

    private DeFiOrderBookUpdate? TryParseOrderBook(string symbol, Stream json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("channel", out var ch) || ch.GetString() != "l2Book")
                return null;

            if (!root.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("levels", out var levels)) return null;

            var bids = ParseLevels(levels.GetArrayLength() > 0 ? levels[0] : default);
            var asks = ParseLevels(levels.GetArrayLength() > 1 ? levels[1] : default);

            return new DeFiOrderBookUpdate(symbol, bids, asks, DateTimeOffset.UtcNow);
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

    private string GetWalletAddress()
    {
        var addr = Environment.GetEnvironmentVariable("HYPERLIQUID_WALLET_ADDRESS");
        if (string.IsNullOrWhiteSpace(addr))
            _logger.LogError(
                "HYPERLIQUID_WALLET_ADDRESS environment variable is not set. " +
                "API calls requiring a wallet address will fail.");
        return addr ?? string.Empty;
    }
}
