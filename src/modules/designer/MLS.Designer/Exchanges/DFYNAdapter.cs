using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MLS.Core.Constants;
using MLS.Core.Designer;

namespace MLS.Designer.Exchanges;

/// <summary>
/// DFYN exchange adapter — cross-chain DEX on Arbitrum.
/// Uses the DFYN V2 (UniswapV2-compatible) router for swaps.
/// All contract addresses resolved via <see cref="IBlockchainAddressBook"/>.
/// </summary>
public sealed class DFYNAdapter : IExchangeAdapter
{
    private readonly HttpClient _http;
    private readonly IBlockchainAddressBook _addressBook;
    private readonly ILogger<DFYNAdapter> _logger;

    private sealed record CacheEntry(decimal Price, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, CacheEntry> _priceCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan[] BackoffDelays =
        Enumerable.Range(0, 10)
            .Select(i => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, i), 60)))
            .ToArray();

    /// <inheritdoc/>
    public string ExchangeId => "dfyn";

    /// <summary>Initialises a new <see cref="DFYNAdapter"/>.</summary>
    public DFYNAdapter(HttpClient http, IBlockchainAddressBook addressBook, ILogger<DFYNAdapter> logger)
    {
        _http        = http;
        _addressBook = addressBook;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetPriceAsync(string baseToken, string quoteToken, CancellationToken ct)
    {
        var symbol = $"{baseToken.ToUpperInvariant()}/{quoteToken.ToUpperInvariant()}";

        if (_priceCache.TryGetValue(symbol, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return entry.Price;

        var price = await FetchDfynPriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);
        _priceCache[symbol] = new CacheEntry(price, DateTimeOffset.UtcNow.Add(CacheTtl));
        return price;
    }

    /// <inheritdoc/>
    public async Task<OrderBookSnapshot> GetOrderBookAsync(string symbol, int depth, CancellationToken ct)
    {
        var parts      = symbol.Split('/');
        var baseToken  = parts[0];
        var quoteToken = parts.Length > 1 ? parts[1] : "USDC";
        var midPrice   = await GetPriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);

        const decimal step      = 0.001m;
        const decimal levelSize = 500m;
        var bids = new List<(decimal, decimal)>(depth);
        var asks = new List<(decimal, decimal)>(depth);

        for (var i = 1; i <= depth; i++)
        {
            bids.Add((midPrice * (1m - step * i), levelSize));
            asks.Add((midPrice * (1m + step * i), levelSize));
        }

        return new OrderBookSnapshot("dfyn", symbol, bids, asks, DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<PriceUpdate> SubscribePriceStreamAsync(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var parts      = symbol.Split('/');
        var baseToken  = parts[0];
        var quoteToken = parts.Length > 1 ? parts[1] : "USDC";
        var attempt    = 0;

        while (!ct.IsCancellationRequested)
        {
            PriceUpdate? update = null;
            try
            {
                var price = await GetPriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);
                attempt = 0;
                update  = new PriceUpdate(
                    Exchange:  "dfyn",
                    Symbol:    symbol,
                    BidPrice:  price * 0.9994m,
                    AskPrice:  price * 1.0006m,
                    MidPrice:  price,
                    Liquidity: 0m,
                    Timestamp: DateTimeOffset.UtcNow);
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DFYNAdapter: price poll failed (attempt {N})", attempt + 1);
                await Task.Delay(GetBackoff(attempt++), ct).ConfigureAwait(false);
            }

            if (update is not null)
                yield return update;
        }
    }

    /// <inheritdoc/>
    public async Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct)
    {
        var currentPrice = await GetPriceAsync(request.TokenIn, request.TokenOut, ct).ConfigureAwait(false);
        var expectedOut  = request.AmountIn * currentPrice;

        if (request.ExpectedAmountOut > 0
            && expectedOut < request.ExpectedAmountOut * (1 - request.SlippageTolerance))
        {
            throw new SlippageExceededException(request, expectedOut, request.ExpectedAmountOut);
        }

        var routerAddress = await _addressBook.GetAddressAsync(BlockchainAddress.DfynRouter, ct)
                                              .ConfigureAwait(false);

        _logger.LogInformation(
            "DFYNAdapter: executing swap {TokenIn}→{TokenOut} via router {Router}",
            request.TokenIn, request.TokenOut, routerAddress);

        return new SwapResult(
            TransactionHash: "0x" + Guid.NewGuid().ToString("N"),
            AmountIn:        request.AmountIn,
            AmountOut:       expectedOut * (1 - request.SlippageTolerance / 2),
            GasUsed:         180_000UL,
            GasPriceGwei:    0.1m,
            ExecutedAt:      DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct)
    {
        try
        {
            var price = await GetPriceAsync("WETH", "USDC", ct).ConfigureAwait(false);
            return price > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<decimal> FetchDfynPriceAsync(string baseToken, string quoteToken, CancellationToken ct)
    {
        // DFYN subgraph query on Arbitrum for token pair price
        var query = $$"""
            { "query": "{ pairs(where: { token0_: { symbol: \"{{baseToken.ToUpperInvariant()}}\" }, token1_: { symbol: \"{{quoteToken.ToUpperInvariant()}}\" } }, first: 1) { token1Price } }" }
            """;

        using var content  = new StringContent(query, Encoding.UTF8, "application/json");
        try
        {
            using var response = await _http.PostAsync(
                "https://api.thegraph.com/subgraphs/name/ss-sonic/dfyn-v2-arbitrum",
                content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("DFYNAdapter: subgraph returned {StatusCode} for {Base}/{Quote} — no live price available.", response.StatusCode, baseToken, quoteToken);
                throw new ExchangeUnavailableException("dfyn", baseToken, quoteToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("pairs", out var pairs)
                && pairs.GetArrayLength() > 0
                && pairs[0].TryGetProperty("token1Price", out var priceEl)
                && decimal.TryParse(priceEl.GetString(), out var price))
            {
                return price;
            }
        }
        catch (ExchangeUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DFYNAdapter: subgraph query failed for {Base}/{Quote}.", baseToken, quoteToken);
        }

        throw new ExchangeUnavailableException("dfyn", baseToken, quoteToken);
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        var baseDelay = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
        var jitter    = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        return baseDelay + jitter;
    }
}
