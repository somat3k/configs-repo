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
/// Supported timeframes: <c>1h</c> (queries <c>poolHourDatas</c>) and
/// <c>1d</c> (queries <c>poolDayDatas</c>).  Other timeframes are rejected.
/// </para>
/// <para>
/// Subgraph endpoint: <c>https://api.thegraph.com/subgraphs/name/camelot-dex/camelot-v3</c>
/// </para>
/// </remarks>
public sealed class CamelotFeedCollector(
    IHttpClientFactory _httpFactory,
    ILogger<CamelotFeedCollector> _logger) : FeedCollector(_logger)
{
    private const string SubgraphUrl =
        "https://api.thegraph.com/subgraphs/name/camelot-dex/camelot-v3";

    /// <summary>
    /// Timeframes supported by the Camelot V3 subgraph.
    /// <c>1h</c> maps to <c>poolHourDatas</c>; <c>1d</c> maps to <c>poolDayDatas</c>.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedTimeframes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1h", "1d" };

    /// <inheritdoc/>
    public override string ExchangeId => "camelot";

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<CandleEntity> StreamCandlesAsync(
        FeedKey key,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!SupportedTimeframes.Contains(key.Timeframe))
        {
            _logger.LogWarning(
                "CamelotFeedCollector: unsupported timeframe '{Timeframe}' for {Symbol} — " +
                "only 1h and 1d are available from the Camelot subgraph",
                HydraUtils.SanitiseFeedId(key.Timeframe),
                HydraUtils.SanitiseFeedId(key.Symbol));
            yield break;
        }

        var pollInterval = HydraUtils.TimeframeToInterval(key.Timeframe);
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
                _logger.LogWarning(ex, "CamelotFeedCollector: subgraph fetch error for {Symbol}",
                    HydraUtils.SanitiseFeedId(key.Symbol));
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
        // Select subgraph entity based on timeframe
        var entityName  = key.Timeframe == "1d" ? "poolDayDatas" : "poolHourDatas";
        var periodStart = since.ToUnixTimeSeconds();

        // Sanitise symbol before embedding in the GraphQL query (strip non-alphanumeric chars)
        var rawSymbol = key.Symbol.ToLowerInvariant().Replace("-", "").Replace("/", "");
        var poolId    = HydraUtils.SanitiseFeedId(rawSymbol);

        // Build via JsonSerializer to avoid any interpolation injection
        var queryBody = new
        {
            query = $"{{ {entityName}(first: 200, orderBy: periodStartUnix, orderDirection: asc," +
                    $" where: {{ pool_: {{ token0_: {{symbol_contains_nocase: \"{poolId}\" }} }}," +
                    $" periodStartUnix_gt: {periodStart} }}) {{" +
                    $" periodStartUnix high low open close volumeToken0 volumeToken1 }} }}"
        };
        var queryJson = System.Text.Json.JsonSerializer.Serialize(queryBody);

        using var content  = new StringContent(queryJson, Encoding.UTF8, "application/json");
        using var http     = _httpFactory.CreateClient("camelot");
        using var response = await http.PostAsync(SubgraphUrl, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return [];

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc          = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                                                    .ConfigureAwait(false);

        return ParseSubgraphCandles(doc.RootElement, key, entityName);
    }

    private static IReadOnlyList<CandleEntity> ParseSubgraphCandles(
        JsonElement root, FeedKey key, string entityName)
    {
        var result = new List<CandleEntity>();

        if (!root.TryGetProperty("data", out var data)) return result;
        if (!data.TryGetProperty(entityName, out var rows)) return result;

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("periodStartUnix", out var tsProp)
                || !tsProp.TryGetInt64(out var tsUnix)) continue;

            var open  = HydraUtils.GetJsonDouble(row, "open");
            var high  = HydraUtils.GetJsonDouble(row, "high");
            var low   = HydraUtils.GetJsonDouble(row, "low");
            var close = HydraUtils.GetJsonDouble(row, "close");
            var vol   = HydraUtils.GetJsonDouble(row, "volumeToken0");

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
                QuoteVolume = HydraUtils.GetJsonDouble(row, "volumeToken1"),
                InsertedAt  = DateTimeOffset.UtcNow,
            });
        }

        return result;
    }
}
