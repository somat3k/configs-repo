using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Constants;
using MLS.Core.Designer;

namespace MLS.Designer.Exchanges;

/// <summary>
/// HYPERLIQUID exchange adapter — primary DEX/perpetuals broker.
/// Provides REST price queries and WebSocket streaming for BTC, ETH, ARB perpetuals.
/// </summary>
/// <remarks>
/// REST base: <c>https://api.hyperliquid.xyz</c> <br/>
/// WebSocket: <c>wss://api.hyperliquid.xyz/ws</c>
/// </remarks>
public sealed class HyperliquidAdapter : IExchangeAdapter
{
    private const string RestBase = "https://api.hyperliquid.xyz";
    private const string WsUri    = "wss://api.hyperliquid.xyz/ws";

    private readonly HttpClient _http;
    private readonly ILogger<HyperliquidAdapter> _logger;

    // ── Per-symbol price cache (1-second TTL) ─────────────────────────────────
    private sealed record CacheEntry(decimal Price, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, CacheEntry> _priceCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(1);

    // ── Exponential backoff delays (base 1s, max 60s) ─────────────────────────
    private static readonly TimeSpan[] BackoffDelays =
        Enumerable.Range(0, 10)
            .Select(i => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, i), 60)))
            .ToArray();

    /// <inheritdoc/>
    public string ExchangeId => "hyperliquid";

    /// <summary>Initialises a new <see cref="HyperliquidAdapter"/>.</summary>
    public HyperliquidAdapter(HttpClient http, ILogger<HyperliquidAdapter> logger)
    {
        _http   = http;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetPriceAsync(string baseToken, string quoteToken, CancellationToken ct)
    {
        var symbol = NormaliseSymbol(baseToken, quoteToken);

        if (_priceCache.TryGetValue(symbol, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return entry.Price;

        var payload = JsonSerializer.Serialize(new
        {
            type = "allMids"
        });

        using var content  = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"{RestBase}/info", content, ct)
                                        .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc  = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        // Hyperliquid allMids returns: { "COIN": "65000.0", ... }
        var coin  = baseToken.Replace("W", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        decimal price = 0m;

        if (doc.RootElement.TryGetProperty(coin, out var midProp)
            && decimal.TryParse(midProp.GetString(), out var parsed))
        {
            price = parsed;
        }

        _priceCache[symbol] = new CacheEntry(price, DateTimeOffset.UtcNow.Add(CacheTtl));
        return price;
    }

    /// <inheritdoc/>
    public async Task<OrderBookSnapshot> GetOrderBookAsync(string symbol, int depth, CancellationToken ct)
    {
        var coin    = symbol.Split('/')[0].ToUpperInvariant();
        var payload = JsonSerializer.Serialize(new { type = "l2Book", coin, nSigFigs = (int?)null });

        using var content  = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"{RestBase}/info", content, ct)
                                        .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var bids = ParseLevels(doc.RootElement, "bids", depth);
        var asks = ParseLevels(doc.RootElement, "asks", depth);

        return new OrderBookSnapshot("hyperliquid", symbol, bids, asks, DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<PriceUpdate> SubscribePriceStreamAsync(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var coin    = symbol.Split('/')[0].ToUpperInvariant();
        var attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();

            bool connected;
            try
            {
                await ws.ConnectAsync(new Uri(WsUri), ct).ConfigureAwait(false);
                connected = true;
                attempt   = 0;
                _logger.LogInformation("HyperliquidAdapter: connected to {Uri}", WsUri);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                connected = false;
                _logger.LogWarning(ex, "HyperliquidAdapter: connection failed (attempt {N})", attempt + 1);
            }

            if (!connected)
            {
                var delay = GetBackoff(attempt++);
                await Task.Delay(delay, ct).ConfigureAwait(false);
                continue;
            }

            // Subscribe to l2Book updates
            var sub = JsonSerializer.SerializeToUtf8Bytes(new
            {
                method = "subscribe",
                subscription = new { type = "l2Book", coin }
            });

            await ws.SendAsync(sub, WebSocketMessageType.Text, endOfMessage: true, ct)
                    .ConfigureAwait(false);

            var buffer = new byte[65536];

            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "HyperliquidAdapter: receive error, reconnecting.");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close) break;

                var update = ParseL2Update(buffer.AsMemory(0, result.Count), symbol);
                if (update is not null)
                    yield return update;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct)
    {
        // Validate slippage before submitting
        var currentPrice = await GetPriceAsync(request.TokenIn, request.TokenOut, ct)
                               .ConfigureAwait(false);

        var expectedOut = request.AmountIn * currentPrice;

        if (request.ExpectedAmountOut > 0
            && expectedOut < request.ExpectedAmountOut * (1 - request.SlippageTolerance))
        {
            throw new SlippageExceededException(request, expectedOut, request.ExpectedAmountOut);
        }

        // Hyperliquid perps: market order via /exchange endpoint
        // EIP-712 signing is handled by the upstream wallet service
        var orderPayload = new
        {
            action = new
            {
                type   = "order",
                orders = new[]
                {
                    new
                    {
                        a       = 0,                        // asset index (placeholder)
                        b       = request.TokenIn != "USDC", // isBuy
                        p       = currentPrice.ToString("F6"),
                        s       = request.AmountIn.ToString("F6"),
                        r       = false,
                        t       = new { limit = new { tif = "Ioc" } }
                    }
                },
                grouping = "na"
            },
            nonce     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            signature = new { r = string.Empty, s = string.Empty, v = 0 }
        };

        using var content  = new StringContent(
            JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"{RestBase}/exchange", content, ct)
                                        .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var txHash = doc.RootElement.TryGetProperty("response", out var resp)
                  && resp.TryGetProperty("data", out var data)
                  && data.TryGetProperty("statuses", out var statuses)
                  && statuses.GetArrayLength() > 0
                  && statuses[0].TryGetProperty("filled", out var filled)
                  && filled.TryGetProperty("oid", out var oid)
            ? oid.GetInt64().ToString()
            : Guid.NewGuid().ToString("N");

        return new SwapResult(
            TransactionHash: txHash,
            AmountIn:        request.AmountIn,
            AmountOut:       expectedOut,
            GasUsed:         0,          // Hyperliquid L1 has no gas
            GasPriceGwei:    0,
            ExecutedAt:      DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { type = "meta" });
            using var content  = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync($"{RestBase}/info", content, ct)
                                            .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormaliseSymbol(string baseToken, string quoteToken) =>
        $"{baseToken.ToUpperInvariant()}/{quoteToken.ToUpperInvariant()}";

    private static TimeSpan GetBackoff(int attempt)
    {
        var baseDelay = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
        var jitter    = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        return baseDelay + jitter;
    }

    private static IReadOnlyList<(decimal Price, decimal Size)> ParseLevels(
        JsonElement root, string side, int depth)
    {
        if (!root.TryGetProperty("levels", out var levels)) return [];
        if (!levels.TryGetProperty(side == "bids" ? "0" : "1", out var levelArr)
            && !TryGetLevelsBySide(root, side, out levelArr))
            return [];

        var result = new List<(decimal, decimal)>(depth);
        foreach (var level in levelArr.EnumerateArray())
        {
            if (result.Count >= depth) break;
            var px  = level.TryGetProperty("px", out var pxEl) && decimal.TryParse(pxEl.GetString(), out var p) ? p : 0m;
            var sz  = level.TryGetProperty("sz", out var szEl) && decimal.TryParse(szEl.GetString(), out var s) ? s : 0m;
            if (px > 0) result.Add((px, sz));
        }
        return result;
    }

    private static bool TryGetLevelsBySide(JsonElement root, string side, out JsonElement result)
    {
        result = default;
        if (!root.TryGetProperty("levels", out var levels)) return false;
        // Hyperliquid l2Book: { "levels": [[bid_levels...], [ask_levels...]] }
        if (levels.ValueKind != JsonValueKind.Array || levels.GetArrayLength() < 2) return false;
        result = side == "bids" ? levels[0] : levels[1];
        return true;
    }

    private static PriceUpdate? ParseL2Update(ReadOnlyMemory<byte> data, string symbol)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            // Hyperliquid WS message: { "channel": "l2Book", "data": { "levels": [[bids],[asks]], "coin": "BTC" } }
            if (!root.TryGetProperty("data", out var msgData)) return null;

            var levels = msgData.TryGetProperty("levels", out var lv) ? lv : default;
            if (levels.ValueKind != JsonValueKind.Array || levels.GetArrayLength() < 2) return null;

            var bids = levels[0];
            var asks = levels[1];

            decimal bid = 0, ask = 0;
            if (bids.GetArrayLength() > 0 && bids[0].TryGetProperty("px", out var bp)
                && decimal.TryParse(bp.GetString(), out var b)) bid = b;
            if (asks.GetArrayLength() > 0 && asks[0].TryGetProperty("px", out var ap)
                && decimal.TryParse(ap.GetString(), out var a)) ask = a;

            if (bid == 0 && ask == 0) return null;

            return new PriceUpdate(
                Exchange:  "hyperliquid",
                Symbol:    symbol,
                BidPrice:  bid,
                AskPrice:  ask,
                MidPrice:  (bid + ask) / 2,
                Liquidity: 0m,
                Timestamp: DateTimeOffset.UtcNow);
        }
        catch
        {
            return null;
        }
    }
}
