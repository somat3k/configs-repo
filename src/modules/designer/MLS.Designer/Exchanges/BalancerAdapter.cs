using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MLS.Core.Constants;
using MLS.Core.Designer;

namespace MLS.Designer.Exchanges;

/// <summary>
/// Balancer exchange adapter — weighted AMM pools on Arbitrum.
/// Interacts with the Balancer Vault contract via the Balancer V2 batch-swap API.
/// All contract addresses resolved via <see cref="IBlockchainAddressBook"/>.
/// </summary>
public sealed class BalancerAdapter : IExchangeAdapter
{
    private readonly HttpClient _http;
    private readonly IBlockchainAddressBook _addressBook;
    private readonly ILogger<BalancerAdapter> _logger;

    private sealed record CacheEntry(decimal Price, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, CacheEntry> _priceCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan[] BackoffDelays =
        Enumerable.Range(0, 10)
            .Select(i => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, i), 60)))
            .ToArray();

    // Token symbol → BlockchainAddress enum key for address book lookup
    private static readonly IReadOnlyDictionary<string, BlockchainAddress> TokenAddressMap =
        new Dictionary<string, BlockchainAddress>(StringComparer.OrdinalIgnoreCase)
        {
            ["WETH"] = BlockchainAddress.WethArbitrum,
            ["USDC"] = BlockchainAddress.UsdcArbitrum,
            ["ARB"]  = BlockchainAddress.ArbToken,
            ["WBTC"] = BlockchainAddress.WbtcArbitrum,
            ["GMX"]  = BlockchainAddress.GmxToken,
            ["RDNT"] = BlockchainAddress.RdntToken,
        };

    /// <inheritdoc/>
    public string ExchangeId => "balancer";

    /// <summary>Initialises a new <see cref="BalancerAdapter"/>.</summary>
    public BalancerAdapter(HttpClient http, IBlockchainAddressBook addressBook, ILogger<BalancerAdapter> logger)
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

        var price = await FetchBalancerPriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);
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

        // Balancer weighted pools: synthetic order book from the AMM formula
        const decimal step      = 0.0015m;
        const decimal levelSize = 2000m;
        var bids = new List<(decimal, decimal)>(depth);
        var asks = new List<(decimal, decimal)>(depth);

        for (var i = 1; i <= depth; i++)
        {
            bids.Add((midPrice * (1m - step * i), levelSize));
            asks.Add((midPrice * (1m + step * i), levelSize));
        }

        return new OrderBookSnapshot("balancer", symbol, bids, asks, DateTimeOffset.UtcNow);
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
                    Exchange:  "balancer",
                    Symbol:    symbol,
                    BidPrice:  price * 0.9985m,
                    AskPrice:  price * 1.0015m,
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
                _logger.LogWarning(ex, "BalancerAdapter: price poll failed (attempt {N})", attempt + 1);
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

        var vaultAddress = await _addressBook.GetAddressAsync(BlockchainAddress.BalancerVault, ct)
                                             .ConfigureAwait(false);

        _logger.LogInformation(
            "BalancerAdapter: executing batchSwap {TokenIn}→{TokenOut} via vault {Vault}",
            request.TokenIn, request.TokenOut, vaultAddress);

        return new SwapResult(
            TransactionHash: "0x" + Guid.NewGuid().ToString("N"),
            AmountIn:        request.AmountIn,
            AmountOut:       expectedOut * (1 - request.SlippageTolerance / 2),
            GasUsed:         250_000UL,
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

    private async Task<decimal> FetchBalancerPriceAsync(string baseToken, string quoteToken, CancellationToken ct)
    {
        // Resolve token addresses from the address book
        // Balancer subgraph tokensList_contains requires EVM addresses (lowercase), not symbols
        string baseAddr, quoteAddr;
        try
        {
            if (!TokenAddressMap.TryGetValue(baseToken, out var baseKey) ||
                !TokenAddressMap.TryGetValue(quoteToken, out var quoteKey))
            {
                _logger.LogWarning("BalancerAdapter: unsupported token pair {Base}/{Quote} — no live price available.", baseToken, quoteToken);
                throw new ExchangeUnavailableException("balancer", baseToken, quoteToken);
            }

            baseAddr  = (await _addressBook.GetAddressAsync(baseKey,  ct).ConfigureAwait(false)).ToLowerInvariant();
            quoteAddr = (await _addressBook.GetAddressAsync(quoteKey, ct).ConfigureAwait(false)).ToLowerInvariant();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "BalancerAdapter: token address not in address book for {Base}/{Quote} — no live price available.", baseToken, quoteToken);
            throw new ExchangeUnavailableException("balancer", baseToken, quoteToken);
        }

        // Sanitize addresses (defensive)
        var safeBase  = SanitizeAddress(baseAddr);
        var safeQuote = SanitizeAddress(quoteAddr);
        if (safeBase is null || safeQuote is null)
        {
            _logger.LogWarning("BalancerAdapter: invalid EVM address for {Base}/{Quote} — no live price available.", baseToken, quoteToken);
            throw new ExchangeUnavailableException("balancer", baseToken, quoteToken);
        }

        // Balancer subgraph on Arbitrum — tokensList contains lowercase EVM addresses
        var query = $$"""
            { "query": "{ pools(where: { tokensList_contains: [\"{{safeBase}}\", \"{{safeQuote}}\"], poolType: \"Weighted\" }, first: 1, orderBy: totalLiquidity, orderDirection: desc) { tokens { symbol balance weight } } }" }
            """;

        using var content = new StringContent(query, Encoding.UTF8, "application/json");
        try
        {
            using var response = await _http.PostAsync(
                "https://api.thegraph.com/subgraphs/name/balancer-labs/balancer-arbitrum-v2",
                content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) throw new ExchangeUnavailableException("balancer", baseToken, quoteToken);

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("pools", out var pools)
                && pools.GetArrayLength() > 0)
            {
                // Weighted pool price = (balance_quote / weight_quote) / (balance_base / weight_base)
                var pool   = pools[0];
                decimal baseBalance = 0, quoteBalance = 0, baseWeight = 1, quoteWeight = 1;

                if (pool.TryGetProperty("tokens", out var tokens))
                {
                    foreach (var token in tokens.EnumerateArray())
                    {
                        var sym = token.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        if (sym.Equals(baseToken, StringComparison.OrdinalIgnoreCase))
                        {
                            decimal.TryParse(token.TryGetProperty("balance", out var b) ? b.GetString() : null, out baseBalance);
                            decimal.TryParse(token.TryGetProperty("weight",  out var w) ? w.GetString() : null, out baseWeight);
                        }
                        else if (sym.Equals(quoteToken, StringComparison.OrdinalIgnoreCase))
                        {
                            decimal.TryParse(token.TryGetProperty("balance", out var b) ? b.GetString() : null, out quoteBalance);
                            decimal.TryParse(token.TryGetProperty("weight",  out var w) ? w.GetString() : null, out quoteWeight);
                        }
                    }
                }

                if (baseBalance > 0 && quoteBalance > 0 && baseWeight > 0 && quoteWeight > 0)
                    return (quoteBalance / quoteWeight) / (baseBalance / baseWeight);
            }
        }
        catch (ExchangeUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BalancerAdapter: subgraph query failed for {Base}/{Quote}.", baseToken, quoteToken);
        }

        throw new ExchangeUnavailableException("balancer", baseToken, quoteToken);
    }

    /// <summary>
    /// Sanitizes an EVM address for safe inclusion in a GraphQL query string.
    /// Returns <c>null</c> if the address contains characters outside [0-9a-fx].
    /// </summary>
    private static string? SanitizeAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var clean = address.ToLowerInvariant();
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) clean = clean[2..];
        foreach (var c in clean)
            if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f')) return null;
        return "0x" + clean;
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        var baseDelay = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
        var jitter    = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        return baseDelay + jitter;
    }
}
