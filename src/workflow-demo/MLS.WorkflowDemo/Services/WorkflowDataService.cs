using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.WorkflowDemo.Models;

namespace MLS.WorkflowDemo.Services;

/// <summary>
/// Pure-functional data-fetch pipeline for the MLS workflow demo.
/// All public members are stateless: they compose an <see cref="HttpClient"/> with
/// an immutable request descriptor and return an immutable result, making them safe
/// to call from any number of concurrent Blazor circuits.
/// </summary>
public sealed class WorkflowDataService(
    IHttpClientFactory httpFactory,
    ILogger<WorkflowDataService> logger)
{
    // ── JSON options (shared, immutable) ──────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly Uri HlInfoUri     = new("https://api.hyperliquid.xyz/info");
    private static readonly Uri LlamaUri      = new("https://api.llama.fi/v2/protocols");

    // ── Core fetch primitives (pure functions) ────────────────────────────────

    /// <summary>POST body to Hyperliquid /info and deserialise to <typeparamref name="T"/>.</summary>
    private static Func<HttpClient, object, CancellationToken, Task<T>> HlPost<T>() =>
        static async (client, body, ct) =>
        {
            using var resp = await client
                .PostAsJsonAsync(HlInfoUri, body, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content
                .ReadFromJsonAsync<T>(JsonOpts, ct)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException($"Null response for {typeof(T).Name}");
        };

    /// <summary>GET a JSON resource and deserialise to <typeparamref name="T"/>.</summary>
    private static Func<HttpClient, string, CancellationToken, Task<T>> HttpGet<T>() =>
        static async (client, url, ct) =>
        {
            using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content
                .ReadFromJsonAsync<T>(JsonOpts, ct)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException($"Null GET response from {url}");
        };

    // ── Pipeline helpers ──────────────────────────────────────────────────────

    private HttpClient CreateClient(string name = "default") =>
        httpFactory.CreateClient(name);

    private async Task<T> SafeAsync<T>(Func<Task<T>> pipeline, Func<T> fallback)
    {
        try   { return await pipeline().ConfigureAwait(false); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "External API unreachable — using built-in fallback data");
            return fallback();
        }
    }

    /// <summary>
    /// Process-stable FNV-1a 32-bit hash. Avoids relying on
    /// <see cref="string.GetHashCode()"/> which is randomised per-process in .NET.
    /// </summary>
    private static int FnvHash(string s)
    {
        unchecked
        {
            const uint fnvPrime  = 16_777_619u;
            uint       hash      = 2_166_136_261u;
            foreach (char c in s)
            {
                hash ^= (byte)(c & 0xFF);
                hash *= fnvPrime;
                hash ^= (byte)(c >> 8);
                hash *= fnvPrime;
            }
            return (int)(hash & 0x7FFF_FFFFu);
        }
    }

    // ── Built-in market snapshot (real Hyperliquid assets, realistic price levels) ──
    // Used when the live API is unreachable (e.g. air-gapped CI). The data structure
    // and pipeline code path is identical to the live path; only the source differs.

    private static IReadOnlyList<AssetMarket> BuiltInMarkets()
    {
        // Representative Hyperliquid perpetuals — prices reflect typical ranges.
        // Funding / OI / vol are proportional to real market structure.
        ReadOnlySpan<(string sym, decimal mid, decimal prev, decimal vol, decimal fund, decimal oi, int lev)> raw =
        [
            ("ETH",   3_441.20m, 3_380.50m, 1_240_000_000m,  0.0082m, 980_000m, 50),
            ("BTC",  68_520.00m,67_800.00m, 2_100_000_000m,  0.0065m, 420_000m, 50),
            ("SOL",     178.40m,   174.90m,   420_000_000m,  0.0095m, 890_000m, 50),
            ("ARB",       1.08m,     1.05m,    94_000_000m,  0.0120m,3_200_000m,50),
            ("AVAX",     36.80m,    35.90m,   110_000_000m,  0.0072m, 620_000m, 50),
            ("MATIC",     0.72m,     0.71m,    78_000_000m,  0.0088m,4_100_000m,50),
            ("LINK",     14.20m,    13.85m,    65_000_000m,  0.0091m,1_800_000m,50),
            ("OP",        2.38m,     2.31m,    52_000_000m,  0.0110m,2_600_000m,50),
            ("NEAR",      6.12m,     5.98m,    44_000_000m,  0.0102m,1_900_000m,50),
            ("INJ",      28.70m,    27.90m,    39_000_000m,  0.0135m,  740_000m,50),
            ("TIA",       8.44m,     8.21m,    33_000_000m,  0.0118m,1_200_000m,50),
            ("APT",      10.80m,    10.55m,    31_000_000m,  0.0099m,1_050_000m,50),
            ("SUI",       1.68m,     1.62m,    28_000_000m,  0.0143m,5_800_000m,50),
            ("WIF",       2.91m,     2.75m,    26_000_000m,  0.0198m,3_300_000m,50),
            ("DOGE",      0.162m,    0.158m,   22_000_000m,  0.0078m,8_200_000m,50),
            ("LTC",      84.20m,    82.50m,    18_000_000m,  0.0055m,  430_000m,50),
            ("ATOM",      8.90m,     8.72m,    16_000_000m,  0.0089m,  980_000m,50),
            ("FTM",       0.88m,     0.85m,    14_000_000m,  0.0164m,4_700_000m,50),
            ("MKR",   2_890.00m, 2_820.00m,    12_000_000m,  0.0044m,   18_000m,10),
            ("AAVE",    112.50m,   109.80m,    10_000_000m,  0.0061m,   58_000m,20),
        ];
        return raw
            .ToArray()
            .Select(static r => new AssetMarket(r.sym, r.mid, r.prev, r.vol, r.fund, r.oi, r.lev))
            .OrderByDescending(static m => m.DayVolumeUsd)
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<Candle> BuiltInCandles(string coin)
    {
        // Generate 60 realistic 1m OHLCV bars by walking from a seed price.
        // FnvHash ensures the sequence is identical across process restarts.
        var markets = BuiltInMarkets();
        var seed    = markets.FirstOrDefault(m => m.Symbol == coin)?.Mid ?? 3_441m;
        var rng     = new Random(FnvHash(coin));
        var candles = new List<Candle>(60);
        var price   = seed * 0.98m;
        var baseMs  = DateTimeOffset.UtcNow.AddMinutes(-60).ToUnixTimeMilliseconds();

        for (int i = 0; i < 60; i++)
        {
            var drift  = (decimal)(rng.NextDouble() - 0.498) * price * 0.0018m;
            var open   = price;
            var close  = Math.Max(open + drift, 0.001m);
            var hiDelta= Math.Abs((decimal)rng.NextDouble() * price * 0.0009m);
            var loDelta= Math.Abs((decimal)rng.NextDouble() * price * 0.0009m);
            var vol    = seed * (decimal)(0.4 + rng.NextDouble() * 0.6) / 100m;
            var tMs    = baseMs + i * 60_000L;
            candles.Add(new Candle(
                tMs + 59_999, tMs,
                open.ToString("F4", CultureInfo.InvariantCulture),
                (Math.Max(open, close) + hiDelta).ToString("F4", CultureInfo.InvariantCulture),
                (Math.Min(open, close) - loDelta).ToString("F4", CultureInfo.InvariantCulture),
                close.ToString("F4", CultureInfo.InvariantCulture),
                vol.ToString("F4", CultureInfo.InvariantCulture),
                rng.Next(12, 180)));
            price = close;
        }
        return candles.AsReadOnly();
    }

    private static IReadOnlyList<DefiProtocol> BuiltInDefiProtocols() =>
    [
        new("Hyperliquid", 3_200_000_000, "Derivatives",   18.4,  ["Arbitrum"]),
        new("Balancer",      680_000_000, "DEX",           -2.1,  ["Ethereum","Arbitrum","Polygon"]),
        new("Morpho",        540_000_000, "Lending",        9.7,  ["Ethereum","Base"]),
        new("Camelot",        98_000_000, "DEX",           14.2,  ["Arbitrum"]),
        new("GMX",           620_000_000, "Derivatives",    3.8,  ["Arbitrum","Avalanche"]),
        new("Aave",        10_800_000_000,"Lending",        1.2,  ["Ethereum","Arbitrum","Polygon"]),
        new("Curve",       3_100_000_000, "DEX",           -0.8,  ["Ethereum","Arbitrum"]),
        new("DFYN",           12_000_000, "DEX",            8.3,  ["Arbitrum","Polygon"]),
        new("Radiant",       140_000_000, "Lending",        5.5,  ["Arbitrum","BNB"]),
        new("Pendle",        420_000_000, "Yield",         11.2,  ["Arbitrum","Ethereum"]),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>All mid prices from Hyperliquid.</summary>
    public Task<IReadOnlyList<AssetMid>> GetAllMidsAsync(CancellationToken ct) =>
        SafeAsync(async () =>
        {
            using var c = CreateClient();
            var raw = await HlPost<Dictionary<string, string>>()(c, new { type = "allMids" }, ct);
            return (IReadOnlyList<AssetMid>)raw
                .Where(static kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(static kv => new AssetMid(
                    kv.Key,
                    decimal.Parse(kv.Value, CultureInfo.InvariantCulture)))
                .OrderBy(static a => a.Symbol)
                .ToList()
                .AsReadOnly();
        }, () => BuiltInMarkets()
            .Select(static m => new AssetMid(m.Symbol, m.Mid))
            .ToList().AsReadOnly());
    public Task<IReadOnlyList<AssetMarket>> GetAssetMarketsAsync(CancellationToken ct) =>
        SafeAsync(async () =>
        {
            using var c = CreateClient();
            var raw = await HlPost<JsonElement[]>()(c, new { type = "metaAndAssetCtxs" }, ct);

            var universe = raw[0]
                .GetProperty("universe")
                .Deserialize<List<UniverseAsset>>(JsonOpts)!;

            var ctxs = raw[1]
                .Deserialize<List<AssetCtx>>(JsonOpts)!;

            static decimal D(string? s) =>
                string.IsNullOrWhiteSpace(s) ? 0m
                : decimal.Parse(s, CultureInfo.InvariantCulture);

            return (IReadOnlyList<AssetMarket>)universe
                .Zip(ctxs, (asset, ctx) => new AssetMarket(
                    Symbol:        asset.Name,
                    Mid:           D(ctx.MidPx ?? ctx.MarkPx),
                    PrevDay:       D(ctx.PrevDayPx),
                    DayVolumeUsd:  D(ctx.DayNtlVlm),
                    Funding:       D(ctx.Funding) * 100m,
                    OpenInterest:  D(ctx.OpenInterest),
                    MaxLeverage:   asset.MaxLeverage))
                .Where(static m => m.Mid > 0m)
                .OrderByDescending(static m => m.DayVolumeUsd)
                .Take(30)
                .ToList()
                .AsReadOnly();
        }, () => BuiltInMarkets());

    /// <summary>Recent 1m candles for a given coin (last 60 bars).</summary>
    public Task<IReadOnlyList<Candle>> GetCandlesAsync(string coin, CancellationToken ct)
    {
        var endMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startMs = endMs - 60 * 60_000L; // 60 minutes
        return SafeAsync(async () =>
        {
            using var c = CreateClient();
            var req = new { type = "candleSnapshot", req = new { coin, interval = "1m", startTime = startMs, endTime = endMs } };
            var raw = await HlPost<List<Candle>>()(c, req, ct);
            return (IReadOnlyList<Candle>)raw.TakeLast(60).ToList().AsReadOnly();
        }, () => BuiltInCandles(coin));
    }

    /// <summary>Selected DeFi protocol entries from DeFi Llama.</summary>
    public Task<IReadOnlyList<DefiProtocol>> GetDefiProtocolsAsync(CancellationToken ct) =>
        SafeAsync(async () =>
        {
            using var c = CreateClient();
            var all = await HttpGet<List<DefiProtocol>>()(c, LlamaUri.ToString(), ct);
            // Filter to protocols relevant to MLS (Arbitrum-adjacent DeFi, no Uniswap)
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Hyperliquid", "Camelot", "Balancer", "Morpho", "GMX",
                "Arbitrum Bridge", "Aave", "DFYN", "Curve", "Radiant", "Pendle"
            };
            return (IReadOnlyList<DefiProtocol>)all
                .Where(p => targets.Contains(p.Name) && (p.Tvl ?? 0) > 0)
                .OrderByDescending(static p => p.Tvl)
                .Take(10)
                .ToList()
                .AsReadOnly();
        }, () => BuiltInDefiProtocols());

    // ── Functional feature-engineering pipeline ───────────────────────────────

    /// <summary>
    /// Derives a <see cref="FeatureVector"/> from a candle slice using the same
    /// 8-feature approach as <c>MLS.DataLayer.FeatureEngineer</c> (RSI-14, MACD signal,
    /// Bollinger-Band position, volume delta, momentum-20, ATR normalised, spread bps,
    /// VWAP distance). The formulas here are a simplified demo variant — the production
    /// implementation in <c>MLS.DataLayer</c> requires a minimum window of 34 candles
    /// and applies Wilder's smoothing in a single pass.
    /// </summary>
    public static FeatureVector EngineerFeatures(string symbol, IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 14)
            return new FeatureVector(symbol, 50m, 0m, 0.5m, 0m, 0m, 0m, 0m, 0m);

        var closes  = candles.Select(static c => c.C).ToArray();
        var volumes = candles.Select(static c => c.V).ToArray();
        var highs   = candles.Select(static c => c.H).ToArray();
        var lows    = candles.Select(static c => c.L).ToArray();

        // RSI-14 (Wilder's)
        var gains  = Enumerable.Range(1, closes.Length - 1).Select(i => Math.Max(closes[i] - closes[i-1], 0m)).ToArray();
        var losses = Enumerable.Range(1, closes.Length - 1).Select(i => Math.Max(closes[i-1] - closes[i], 0m)).ToArray();
        var avgGain = gains.TakeLast(14).Average();
        var avgLoss = losses.TakeLast(14).Average();
        var rsi     = avgLoss == 0m ? 100m : 100m - 100m / (1m + avgGain / avgLoss);

        // MACD signal (12/26/9 EMA approximation)
        static decimal Ema(IReadOnlyList<decimal> src, int period)
        {
            var k = 2m / (period + 1);
            return src.Aggregate((ema, val) => ema + k * (val - ema));
        }
        var ema12 = Ema(closes, 12);
        var ema26 = Ema(closes, 26);
        var macdLine   = ema12 - ema26;
        var macdSignal = macdLine * 0.9m; // simplified signal

        // Bollinger Band position
        var last20  = closes.TakeLast(20).ToArray();
        var sma20   = last20.Average();
        var stdDev  = (decimal)Math.Sqrt((double)last20.Select(c => (c - sma20) * (c - sma20)).Average());
        var upper   = sma20 + 2m * stdDev;
        var lower   = sma20 - 2m * stdDev;
        var bbRange = upper - lower;
        var bbPos   = bbRange == 0m ? 0.5m : Math.Clamp((closes[^1] - lower) / bbRange, 0m, 1m);

        // Volume delta %
        var volAvg    = volumes.TakeLast(20).Average();
        var volDelta  = volAvg == 0m ? 0m : (volumes[^1] - volAvg) / volAvg * 100m;

        // Momentum-20
        var momentum = closes.Length >= 20
            ? (closes[^1] - closes[^20]) / closes[^20]
            : 0m;

        // ATR (normalised)
        var atr = Enumerable.Range(1, Math.Min(14, candles.Count - 1))
            .Select(i => Math.Max(highs[i] - lows[i], Math.Max(
                Math.Abs(highs[i] - closes[i-1]),
                Math.Abs(lows[i]  - closes[i-1]))))
            .Average();
        var atrNorm = closes[^1] == 0m ? 0m : atr / closes[^1];

        // Spread bps (ask-bid approximation from high-low of last candle)
        var spreadBps = closes[^1] == 0m ? 0m
            : (candles[^1].H - candles[^1].L) / closes[^1] * 10_000m;

        // VWAP distance
        var vwap = volumes.Sum() == 0m ? closes[^1]
            : closes.Zip(volumes, static (c, v) => c * v).Sum() / volumes.Sum();
        var vwapDist = vwap == 0m ? 0m : (closes[^1] - vwap) / vwap;

        return new FeatureVector(
            Symbol:      symbol,
            Rsi14:       Math.Round(rsi, 2),
            MacdSignal:  Math.Round(macdSignal, 4),
            BbPosition:  Math.Round(bbPos, 4),
            VolumeDelta: Math.Round(volDelta, 2),
            Momentum20:  Math.Round(momentum, 4),
            AtrNorm:     Math.Round(atrNorm, 6),
            SpreadBps:   Math.Round(spreadBps, 2),
            VwapDistance:Math.Round(vwapDist, 6));
    }

    /// <summary>
    /// Derives arbitrage opportunities from a set of asset mids by simulating
    /// a second-venue mid price with a realistic market-microstructure offset.
    /// </summary>
    public static IReadOnlyList<ArbOpportunity> ComputeArbOpportunities(
        IReadOnlyList<AssetMarket> markets)
    {
        // Deterministic venue offset derived from FNV-1a hash (stable across process restarts).
        static decimal VenueOffset(string symbol, string venue) =>
            (FnvHash(symbol + venue) & 0xFF) / 100_000m; // ±0.00001 to ±0.00255 — realistic for CEX/DEX spread

        var venues = new[] { ("Camelot", 0.80m), ("DFYN", 0.55m), ("Hyperliquid", 1.2m), ("nHOP", 0.40m) };

        return markets
            .Take(15)
            .SelectMany(m => venues.Select(v =>
            {
                var (venueName, _) = v;
                var offset   = VenueOffset(m.Symbol, venueName);
                var venuePrice = m.Mid * (1m + offset);
                return (m, venueName, v.Item2, venuePrice);
            }))
            .GroupBy(x => x.m.Symbol)
            .Select(g =>
            {
                var best  = g.OrderByDescending(x => x.venuePrice).First();
                var worst = g.OrderBy(x => x.venuePrice).First();
                var spread = best.m.Mid == 0m ? 0m
                    : (best.venuePrice - worst.venuePrice) / worst.venuePrice * 10_000m;
                var gasUsd = 1.20m + (decimal)(FnvHash(g.Key) & 0x3F) / 100m;
                var notional = worst.venuePrice * 1m; // 1 unit
                var grossPnl = best.venuePrice - worst.venuePrice;
                return new ArbOpportunity(
                    Symbol:       g.Key,
                    BuyVenue:     worst.Item2,
                    BuyPrice:     Math.Round(worst.venuePrice, 4),
                    SellVenue:    best.Item2,
                    SellPrice:    Math.Round(best.venuePrice, 4),
                    SpreadBps:    Math.Round(spread, 2),
                    EstGasUsd:    Math.Round(gasUsd, 2),
                    NetProfitUsd: Math.Round(grossPnl - gasUsd / (notional == 0m ? 1m : notional), 6));
            })
            .Where(static o => o.SpreadBps > 0.5m)
            .OrderByDescending(static o => o.SpreadBps)
            .Take(12)
            .ToList()
            .AsReadOnly();
    }
}
