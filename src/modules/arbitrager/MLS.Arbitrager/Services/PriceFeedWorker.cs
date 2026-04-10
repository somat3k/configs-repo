using System.Text.Json;
using MLS.Arbitrager.Configuration;
using MLS.Arbitrager.Scanning;

namespace MLS.Arbitrager.Services;

/// <summary>
/// Background service that polls price feeds from Hyperliquid REST API and
/// publishes <see cref="PriceSnapshot"/> ticks to <see cref="IOpportunityScanner"/>.
/// </summary>
/// <remarks>
/// <para>
/// Hyperliquid's <c>allMids</c> endpoint returns coins <em>without</em> a leading <c>W</c>
/// (e.g. <c>ETH</c>, <c>BTC</c>). <see cref="ToHyperliquidCoin"/> strips the prefix so lookups work.
/// </para>
/// <para>
/// Cross-rate pairs (e.g. <c>WETH/ARB</c>) are computed as
/// <c>mid_WETH_USD / mid_ARB_USD</c> rather than reusing a single base-token price.
/// </para>
/// <para>
/// Production: replace HTTP polling with WebSocket subscriptions from each exchange adapter.
/// </para>
/// </remarks>
public sealed class PriceFeedWorker(
    IOpportunityScanner _scanner,
    IOptions<ArbitragerOptions> _options,
    IHttpClientFactory _httpFactory,
    ILogger<PriceFeedWorker> _logger) : BackgroundService
{
    // Token pairs to scan across all exchanges
    private static readonly (string Base, string Quote)[] WatchedPairs =
    [
        ("WETH", "USDC"),
        ("ARB",  "USDC"),
        ("WBTC", "USDC"),
        ("WETH", "ARB"),
        ("GMX",  "USDC"),
        ("RDNT", "USDC"),
    ];

    // Bid-ask half-spread assumed for Hyperliquid (CEX — tightest spread)
    private const decimal HyperliquidHalfSpread = 0.0005m;

    // Default liquidity estimate for Hyperliquid in USD (conservative)
    private const decimal DefaultHyperliquidLiquidityUsd = 100_000m;

    // Synthetic DEX liquidity estimates per exchange (USD)
    private const decimal CamelotLiquidityUsd  = 50_000m;
    private const decimal DfynLiquidityUsd     = 30_000m;
    private const decimal BalancerLiquidityUsd = 80_000m;

    // Synthetic DEX price offsets relative to reference mid-price
    private const decimal CamelotMidPremium   = 0.001m;   // +0.10%
    private const decimal DfynMidDiscount     = 0.0005m;  // −0.05%
    private const decimal BalancerMidPremium  = 0.0008m;  // +0.08%
    private const decimal SyntheticSpread     = 0.0004m;  // ±0.04% half-spread

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(opts.ScanIntervalMs));

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                await PollPricesAsync(opts, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PriceFeedWorker: price poll failed — retrying next tick.");
            }
        }
    }

    // ── Price polling ─────────────────────────────────────────────────────────

    private async Task PollPricesAsync(ArbitragerOptions opts, CancellationToken ct)
    {
        var hlPrices = await FetchHyperliquidPricesAsync(opts.HyperliquidRestUrl, ct)
                           .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;

        foreach (var (baseToken, quoteToken) in WatchedPairs)
        {
            // Derive Hyperliquid coin identifiers (strip leading 'W', upper-case)
            var baseCoin  = ToHyperliquidCoin(baseToken);
            var quoteCoin = ToHyperliquidCoin(quoteToken);

            if (!hlPrices.TryGetValue(baseCoin, out var baseMidUsd) || baseMidUsd <= 0)
                continue;

            // Compute the correct cross-rate:
            // If quote is USDC the USD price IS the cross-rate.
            // For non-USD quotes compute baseMidUsd / quoteMidUsd.
            decimal crossMid;
            if (string.Equals(quoteToken, "USDC", StringComparison.OrdinalIgnoreCase))
            {
                crossMid = baseMidUsd;
            }
            else
            {
                if (!hlPrices.TryGetValue(quoteCoin, out var quoteMidUsd) || quoteMidUsd <= 0)
                    continue;
                crossMid = baseMidUsd / quoteMidUsd;
            }

            var symbol = $"{baseToken}/{quoteToken}";

            // Publish Hyperliquid price (CEX — tightest spread)
            _scanner.PublishPrice(new PriceSnapshot(
                Exchange:     "hyperliquid",
                Symbol:       symbol,
                BidPrice:     crossMid * (1m - HyperliquidHalfSpread),
                AskPrice:     crossMid * (1m + HyperliquidHalfSpread),
                MidPrice:     crossMid,
                LiquidityUsd: DefaultHyperliquidLiquidityUsd,
                Timestamp:    now));

            // Publish synthetic DEX prices computed from the same reference cross-rate
            PublishSyntheticDexPrices(symbol, crossMid, now);
        }
    }

    private void PublishSyntheticDexPrices(string symbol, decimal refPrice, DateTimeOffset now)
    {
        // Camelot: slight premium (0.1%)
        var camelotMid = refPrice * (1m + CamelotMidPremium);
        _scanner.PublishPrice(new PriceSnapshot(
            Exchange:     "camelot",
            Symbol:       symbol,
            BidPrice:     camelotMid * (1m - SyntheticSpread),
            AskPrice:     camelotMid * (1m + SyntheticSpread),
            MidPrice:     camelotMid,
            LiquidityUsd: CamelotLiquidityUsd,
            Timestamp:    now));

        // DFYN: slight discount (0.05%)
        var dfynMid = refPrice * (1m - DfynMidDiscount);
        _scanner.PublishPrice(new PriceSnapshot(
            Exchange:     "dfyn",
            Symbol:       symbol,
            BidPrice:     dfynMid * (1m - SyntheticSpread),
            AskPrice:     dfynMid * (1m + SyntheticSpread),
            MidPrice:     dfynMid,
            LiquidityUsd: DfynLiquidityUsd,
            Timestamp:    now));

        // Balancer: weighted pool, slight premium (0.08%)
        var balancerMid = refPrice * (1m + BalancerMidPremium);
        _scanner.PublishPrice(new PriceSnapshot(
            Exchange:     "balancer",
            Symbol:       symbol,
            BidPrice:     balancerMid * (1m - SyntheticSpread),
            AskPrice:     balancerMid * (1m + SyntheticSpread),
            MidPrice:     balancerMid,
            LiquidityUsd: BalancerLiquidityUsd,
            Timestamp:    now));
    }

    private async Task<Dictionary<string, decimal>> FetchHyperliquidPricesAsync(
        string baseUrl, CancellationToken ct)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var http = _httpFactory.CreateClient("price-feed");
            using var cts  = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Hyperliquid REST: POST /info with {"type":"allMids"}
            var body = new StringContent(
                "{\"type\":\"allMids\"}",
                System.Text.Encoding.UTF8,
                "application/json");

            using var response = await http.PostAsync($"{baseUrl}/info", body, cts.Token)
                                            .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return result;

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token)
                                                            .ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, default, cts.Token)
                                               .ConfigureAwait(false);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String
                    && decimal.TryParse(prop.Value.GetString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var price))
                {
                    result[prop.Name] = price;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "PriceFeedWorker: Hyperliquid fetch failed.");
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a MLS token symbol to the Hyperliquid coin ID.
    /// Hyperliquid uses bare coin names without a leading <c>W</c>
    /// (e.g. <c>WETH</c> → <c>ETH</c>, <c>WBTC</c> → <c>BTC</c>, <c>USDC</c> → <c>USDC</c>).
    /// </summary>
    private static string ToHyperliquidCoin(string token) =>
        token.ToUpperInvariant() switch
        {
            "WETH" => "ETH",
            "WBTC" => "BTC",
            _      => token.ToUpperInvariant(),
        };
}
