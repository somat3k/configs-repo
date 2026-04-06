using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MLS.DataLayer.Persistence;

namespace MLS.DataLayer.Hydra;

/// <summary>
/// Feed collector for the Camelot DEX (Arbitrum).
/// Polls on-chain OHLCV data by querying the Camelot subgraph REST API
/// (hosted on The Graph) using a periodic timer.
/// </summary>
/// <remarks>
/// <para>
/// Camelot does not expose a native WebSocket candle feed.  This collector
/// simulates a live feed by polling the subgraph at each timeframe interval
/// and yielding any new candle that has not yet been stored.
/// </para>
/// <para>
/// Subgraph endpoint: <c>https://api.thegraph.com/subgraphs/name/camelot-dex/camelot-v3</c>
/// </para>
/// </remarks>
public sealed class CamelotFeedCollector(
    HttpClient _http,
    ILogger<CamelotFeedCollector> _logger) : FeedCollector(_logger)
{
    private const string SubgraphUrl =
        "https://api.thegraph.com/subgraphs/name/camelot-dex/camelot-v3";

    /// <inheritdoc/>
    public override string ExchangeId => "camelot";

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<CandleEntity> StreamCandlesAsync(
        FeedKey key,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var pollInterval = TimeframeToInterval(key.Timeframe);
        var lastSeen     = DateTimeOffset.UtcNow - pollInterval * 2; // seed to fetch current candle

        using var timer = new PeriodicTimer(pollInterval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            IReadOnlyList<CandleEntity> candles;
            try
            {
                candles = await FetchCandlesAsync(key, lastSeen, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CamelotFeedCollector: subgraph fetch error for {Symbol}", key.Symbol);
                yield break; // triggers reconnect via FeedCollector base backoff
            }

            foreach (var c in candles)
            {
                if (c.OpenTime > lastSeen)
                    lastSeen = c.OpenTime;
                yield return c;
            }
        }
    }

    // ── Subgraph query ────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<CandleEntity>> FetchCandlesAsync(
        FeedKey key, DateTimeOffset since, CancellationToken ct)
    {
        // Camelot V3 subgraph: query poolDayData / poolHourData
        var periodStart = since.ToUnixTimeSeconds();
        var poolId      = key.Symbol.ToLowerInvariant().Replace("-", "").Replace("/", "");

        var query = $$"""
            {
              "query": "{ poolHourDatas(first: 200, orderBy: periodStartUnix, orderDirection: asc,
                where: { pool_: { token0_: {symbol_contains_nocase: \"{{poolId}}\" } }, periodStartUnix_gt: {{periodStart}} }) {
                  periodStartUnix high low open close volumeToken0 volumeToken1
              } }"
            }
            """;

        using var content  = new StringContent(query, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(SubgraphUrl, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return [];

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc          = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                                                    .ConfigureAwait(false);

        return ParseSubgraphCandles(doc.RootElement, key);
    }

    private static IReadOnlyList<CandleEntity> ParseSubgraphCandles(JsonElement root, FeedKey key)
    {
        var result = new List<CandleEntity>();

        if (!root.TryGetProperty("data", out var data)) return result;
        if (!data.TryGetProperty("poolHourDatas", out var rows)) return result;

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("periodStartUnix", out var tsProp)
                || !tsProp.TryGetInt64(out var tsUnix)) continue;

            static double Dp(JsonElement el, string k)
                => el.TryGetProperty(k, out var v)
                   && double.TryParse(v.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0.0;

            var open  = Dp(row, "open");
            var high  = Dp(row, "high");
            var low   = Dp(row, "low");
            var close = Dp(row, "close");
            var vol   = Dp(row, "volumeToken0");

            result.Add(new CandleEntity
            {
                Exchange    = key.Exchange,
                Symbol      = key.Symbol,
                Timeframe   = key.Timeframe,
                OpenTime    = DateTimeOffset.FromUnixTimeSeconds(tsUnix),
                Open        = open,
                High        = high,
                Low         = low,
                Close       = close,
                Volume      = vol,
                QuoteVolume = Dp(row, "volumeToken1"),
                InsertedAt  = DateTimeOffset.UtcNow,
            });
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TimeSpan TimeframeToInterval(string tf) => tf switch
    {
        "1m"  => TimeSpan.FromMinutes(1),
        "3m"  => TimeSpan.FromMinutes(3),
        "5m"  => TimeSpan.FromMinutes(5),
        "15m" => TimeSpan.FromMinutes(15),
        "30m" => TimeSpan.FromMinutes(30),
        "1h"  => TimeSpan.FromHours(1),
        "4h"  => TimeSpan.FromHours(4),
        "1d"  => TimeSpan.FromDays(1),
        _     => TimeSpan.FromMinutes(1),
    };
}
