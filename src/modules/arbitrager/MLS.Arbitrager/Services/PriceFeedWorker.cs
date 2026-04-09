using System.Text.Json;
using MLS.Arbitrager.Configuration;
using MLS.Arbitrager.Scanning;

namespace MLS.Arbitrager.Services;

/// <summary>
/// Background service that polls price feeds from Hyperliquid REST API and
/// a lightweight Camelot/DFYN/Balancer price estimate endpoint, then publishes
/// <see cref="PriceSnapshot"/> ticks to <see cref="IOpportunityScanner"/>.
/// </summary>
/// <remarks>
/// Production: replace HTTP polling with WebSocket subscriptions from each exchange adapter.
/// The module architecture is designed so <see cref="IOpportunityScanner.PublishPrice"/>
/// can be called from any number of concurrent feed workers.
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

    private static readonly string[] Exchanges = ["hyperliquid", "camelot", "dfyn", "balancer"];

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
        // Poll Hyperliquid REST (allMids endpoint returns all mid prices)
        var hlPrices = await FetchHyperliquidPricesAsync(opts.HyperliquidRestUrl, ct)
                           .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;

        foreach (var (baseToken, quoteToken) in WatchedPairs)
        {
            var symbol = $"{baseToken}/{quoteToken}";

            // Publish Hyperliquid price (CEX — tightest spread)
            if (hlPrices.TryGetValue(baseToken, out var hlMid) && hlMid > 0)
            {
                _scanner.PublishPrice(new PriceSnapshot(
                    Exchange:     "hyperliquid",
                    Symbol:       symbol,
                    BidPrice:     hlMid * 0.9995m,
                    AskPrice:     hlMid * 1.0005m,
                    MidPrice:     hlMid,
                    LiquidityUsd: 100_000m,  // default liquidity estimate
                    Timestamp:    now));
            }

            // For DEX exchanges: use Hyperliquid mid ± synthetic spread to simulate DEX pricing.
            // In production each exchange adapter would call its own price source.
            if (hlPrices.TryGetValue(baseToken, out var refPrice) && refPrice > 0)
            {
                PublishSyntheticDexPrices(symbol, refPrice, now);
            }
        }
    }

    private void PublishSyntheticDexPrices(string symbol, decimal refPrice, DateTimeOffset now)
    {
        // Camelot: slight premium (0.1%)
        _scanner.PublishPrice(new PriceSnapshot(
            Exchange:     "camelot",
            Symbol:       symbol,
            BidPrice:     refPrice * 1.0008m,
            AskPrice:     refPrice * 1.0012m,
            MidPrice:     refPrice * 1.001m,
            LiquidityUsd: 50_000m,
            Timestamp:    now));

        // DFYN: slight discount (0.05%)
        _scanner.PublishPrice(new PriceSnapshot(
            Exchange:     "dfyn",
            Symbol:       symbol,
            BidPrice:     refPrice * 0.9993m,
            AskPrice:     refPrice * 0.9997m,
            MidPrice:     refPrice * 0.9995m,
            LiquidityUsd: 30_000m,
            Timestamp:    now));

        // Balancer: weighted pool, slight premium (0.08%)
        _scanner.PublishPrice(new PriceSnapshot(
            Exchange:     "balancer",
            Symbol:       symbol,
            BidPrice:     refPrice * 1.0006m,
            AskPrice:     refPrice * 1.001m,
            MidPrice:     refPrice * 1.0008m,
            LiquidityUsd: 80_000m,
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
}
