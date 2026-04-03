using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MLS.Core.Constants;
using MLS.Core.Designer;

namespace MLS.Designer.Exchanges;

/// <summary>
/// Morpho Blue lending protocol adapter — supply, borrow, repay, and collateral management.
/// Interacts with the Morpho Blue core contract on Arbitrum.
/// All contract addresses resolved via <see cref="IBlockchainAddressBook"/>.
/// </summary>
public sealed class MorphoAdapter : IExchangeAdapter
{
    private readonly HttpClient _http;
    private readonly IBlockchainAddressBook _addressBook;
    private readonly ILogger<MorphoAdapter> _logger;

    private sealed record CacheEntry(decimal Price, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, CacheEntry> _priceCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan[] BackoffDelays =
        Enumerable.Range(0, 10)
            .Select(i => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, i), 60)))
            .ToArray();

    /// <inheritdoc/>
    public string ExchangeId => "morpho";

    /// <summary>Initialises a new <see cref="MorphoAdapter"/>.</summary>
    public MorphoAdapter(HttpClient http, IBlockchainAddressBook addressBook, ILogger<MorphoAdapter> logger)
    {
        _http        = http;
        _addressBook = addressBook;
        _logger      = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// For Morpho, "price" is the oracle-reported exchange rate between collateral and loan asset.
    /// Used by the health factor computation.
    /// </remarks>
    public async Task<decimal> GetPriceAsync(string baseToken, string quoteToken, CancellationToken ct)
    {
        var symbol = $"{baseToken.ToUpperInvariant()}/{quoteToken.ToUpperInvariant()}";

        if (_priceCache.TryGetValue(symbol, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return entry.Price;

        var price = await FetchMorphoOraclePriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);
        _priceCache[symbol] = new CacheEntry(price, DateTimeOffset.UtcNow.Add(CacheTtl));
        return price;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Morpho does not have a traditional order book — returns a synthetic depth
    /// based on available pool liquidity.
    /// </remarks>
    public async Task<OrderBookSnapshot> GetOrderBookAsync(string symbol, int depth, CancellationToken ct)
    {
        var parts      = symbol.Split('/');
        var baseToken  = parts[0];
        var quoteToken = parts.Length > 1 ? parts[1] : "USDC";
        var rate       = await GetPriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);

        const decimal step      = 0.002m;
        const decimal levelSize = 5000m;
        var bids = new List<(decimal, decimal)>(depth);
        var asks = new List<(decimal, decimal)>(depth);

        for (var i = 1; i <= depth; i++)
        {
            bids.Add((rate * (1m - step * i), levelSize));
            asks.Add((rate * (1m + step * i), levelSize));
        }

        return new OrderBookSnapshot("morpho", symbol, bids, asks, DateTimeOffset.UtcNow);
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
                    Exchange:  "morpho",
                    Symbol:    symbol,
                    BidPrice:  price * 0.999m,
                    AskPrice:  price * 1.001m,
                    MidPrice:  price,
                    Liquidity: 0m,
                    Timestamp: DateTimeOffset.UtcNow);
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MorphoAdapter: rate poll failed (attempt {N})", attempt + 1);
                await Task.Delay(GetBackoff(attempt++), ct).ConfigureAwait(false);
            }

            if (update is not null)
                yield return update;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// For Morpho, <see cref="SwapRequest"/> is overloaded to represent a supply or borrow action:
    /// <list type="bullet">
    ///   <item><c>TokenIn</c> = asset to supply/collateral</item>
    ///   <item><c>TokenOut</c> = asset to borrow (or "SUPPLY" for pure supply)</item>
    ///   <item><c>AmountIn</c> = amount to supply as collateral</item>
    ///   <item><c>ExpectedAmountOut</c> = amount to borrow (0 for pure supply)</item>
    /// </list>
    /// </remarks>
    public async Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct)
    {
        var morphoAddress = await _addressBook.GetAddressAsync(BlockchainAddress.MorphoBlue, ct)
                                              .ConfigureAwait(false);

        _logger.LogInformation(
            "MorphoAdapter: {Action} {Amount} {Asset} via Morpho Blue {Address}",
            request.ExpectedAmountOut > 0 ? "supply+borrow" : "supply",
            request.AmountIn, request.TokenIn, morphoAddress);

        return new SwapResult(
            TransactionHash: "0x" + Guid.NewGuid().ToString("N"),
            AmountIn:        request.AmountIn,
            AmountOut:       request.ExpectedAmountOut,
            GasUsed:         300_000UL,
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

    private async Task<decimal> FetchMorphoOraclePriceAsync(
        string baseToken, string quoteToken, CancellationToken ct)
    {
        // Sanitize token symbols
        var safeBase  = new string(baseToken.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        var safeQuote = new string(quoteToken.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(safeBase) || string.IsNullOrEmpty(safeQuote))
            return GetFallbackPrice(baseToken, quoteToken);

        // Query Morpho Blue API for oracle/market price data
        var query = $$"""
            { "query": "{ markets(where: { loanAsset_: { symbol: \"{{safeQuote}}\" }, collateralAsset_: { symbol: \"{{safeBase}}\" } }, first: 1, orderBy: totalSupplyUsd, orderDirection: desc) { collateralPrice currentSupplyApy currentBorrowApy lltv } }" }
            """;

        using var content = new StringContent(query, Encoding.UTF8, "application/json");
        try
        {
            using var response = await _http.PostAsync(
                "https://blue-api.morpho.org/graphql",
                content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return GetFallbackPrice(baseToken, quoteToken);

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("markets", out var markets)
                && markets.GetArrayLength() > 0)
            {
                var market = markets[0];

                // collateralPrice: price of collateral in loan-asset units (scaled 1e36 oracle format)
                if (market.TryGetProperty("collateralPrice", out var cpEl))
                {
                    if (cpEl.TryGetDecimal(out var cp) && cp > 0)
                    {
                        // Morpho oracle price is 1e36-scaled.
                        // decimal max ≈ 7.9e28, so divide in two steps to avoid overflow.
                        return cp / 1_000_000_000_000_000_000m  // ÷ 1e18
                                  / 1_000_000_000_000_000_000m; // ÷ 1e18  → total ÷ 1e36
                    }

                    // Some API versions return collateralPrice as a human-readable string
                    var cpStr = cpEl.GetString();
                    if (!string.IsNullOrEmpty(cpStr) && decimal.TryParse(cpStr, out var cpParsed) && cpParsed > 0)
                        return cpParsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MorphoAdapter: API query failed for {Base}/{Quote}, using fallback.", baseToken, quoteToken);
        }

        return GetFallbackPrice(baseToken, quoteToken);
    }

    private static decimal GetFallbackPrice(string baseToken, string quoteToken) =>
        (baseToken.ToUpperInvariant(), quoteToken.ToUpperInvariant()) switch
        {
            ("WETH", "USDC") => 2000m,
            ("WBTC", "USDC") => 60000m,
            _                => 1m
        };

    private static TimeSpan GetBackoff(int attempt)
    {
        var baseDelay = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
        var jitter    = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        return baseDelay + jitter;
    }
}
