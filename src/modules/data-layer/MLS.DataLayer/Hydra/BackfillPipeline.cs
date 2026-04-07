using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using MLS.DataLayer.Configuration;
using MLS.DataLayer.Persistence;
using MLS.DataLayer.Services;

namespace MLS.DataLayer.Hydra;

/// <summary>Describes a detected candle data gap that needs to be filled.</summary>
/// <param name="Key">Feed tuple the gap belongs to.</param>
/// <param name="GapStart">Start of the missing window (UTC, inclusive).</param>
/// <param name="GapEnd">End of the missing window (UTC, exclusive).</param>
/// <param name="MissingCandles">Estimated number of missing candles.</param>
public sealed record GapRange(
    FeedKey Key,
    DateTimeOffset GapStart,
    DateTimeOffset GapEnd,
    int MissingCandles);

/// <summary>
/// Hosted background service that fills detected data gaps by fetching historical
/// candles from exchange REST APIs and persisting them to PostgreSQL.
/// Emits <c>DATA_GAP_FILLED</c> on completion.
/// </summary>
/// <remarks>
/// <para>
/// Jobs are enqueued via <see cref="EnqueueAsync"/> (called from <see cref="GapDetector"/>)
/// and processed one at a time by the internal consumer loop.
/// </para>
/// <para>
/// HYPERLIQUID REST endpoint used for backfill:
/// <c>POST https://api.hyperliquid.xyz/info</c> with <c>{"type":"candleSnapshot","req":{...}}</c>.
/// </para>
/// </remarks>
public sealed class BackfillPipeline : BackgroundService
{
    private const string ModuleId = "data-layer";

    private readonly Channel<GapRange> _queue;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptions<DataLayerOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnvelopeSender _envelopeSender;
    private readonly ILogger<BackfillPipeline> _logger;

    /// <summary>Initialises a new <see cref="BackfillPipeline"/>.</summary>
    public BackfillPipeline(
        IHttpClientFactory httpFactory,
        IOptions<DataLayerOptions> options,
        IServiceScopeFactory scopeFactory,
        IEnvelopeSender envelopeSender,
        ILogger<BackfillPipeline> logger)
    {
        _httpFactory    = httpFactory;
        _options        = options;
        _scopeFactory   = scopeFactory;
        _envelopeSender = envelopeSender;
        _logger         = logger;

        var capacity = Math.Max(1, options.Value.BackfillQueueCapacity);
        _queue = Channel.CreateBounded<GapRange>(new BoundedChannelOptions(capacity)
        {
            FullMode          = BoundedChannelFullMode.DropOldest,
            SingleReader      = true,
            SingleWriter      = false,
        });
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enqueues a <paramref name="gap"/> for backfill processing.
    /// If the channel is full the oldest pending job is dropped (see <c>DropOldest</c> policy).
    /// </summary>
    public async ValueTask EnqueueAsync(GapRange gap, CancellationToken ct = default)
    {
        await _queue.Writer.WriteAsync(gap, ct).ConfigureAwait(false);
        _logger.LogDebug(
            "BackfillPipeline: enqueued gap [{Exchange}/{Symbol}/{Timeframe}] {Start}→{End}",
            HydraUtils.SanitiseFeedId(gap.Key.Exchange),
            HydraUtils.SanitiseFeedId(gap.Key.Symbol),
            HydraUtils.SanitiseFeedId(gap.Key.Timeframe),
            gap.GapStart, gap.GapEnd);
    }

    // ── BackgroundService ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var gap in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                await ProcessGapAsync(gap, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "BackfillPipeline: unhandled error filling [{Exchange}/{Symbol}/{Timeframe}]",
                    HydraUtils.SanitiseFeedId(gap.Key.Exchange),
                    HydraUtils.SanitiseFeedId(gap.Key.Symbol),
                    HydraUtils.SanitiseFeedId(gap.Key.Timeframe));
            }
        }
    }

    // ── Core backfill logic ───────────────────────────────────────────────────

    private async Task ProcessGapAsync(GapRange gap, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "BackfillPipeline: filling [{Exchange}/{Symbol}/{Timeframe}] {Start}→{End} (~{Missing} candles)",
            HydraUtils.SanitiseFeedId(gap.Key.Exchange),
            HydraUtils.SanitiseFeedId(gap.Key.Symbol),
            HydraUtils.SanitiseFeedId(gap.Key.Timeframe),
            gap.GapStart, gap.GapEnd, gap.MissingCandles);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository        = scope.ServiceProvider.GetRequiredService<CandleRepository>();

        var inserted   = 0;
        var chunkStart = gap.GapStart;
        var chunkSize  = _options.Value.BackfillChunkSize;

        while (chunkStart < gap.GapEnd && !ct.IsCancellationRequested)
        {
            IReadOnlyList<CandleEntity> batch;

            try
            {
                batch = await FetchChunkAsync(gap.Key, chunkStart, gap.GapEnd, chunkSize, ct)
                            .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "BackfillPipeline: fetch error for [{Exchange}/{Symbol}/{Timeframe}] at {ChunkStart}",
                    HydraUtils.SanitiseFeedId(gap.Key.Exchange),
                    HydraUtils.SanitiseFeedId(gap.Key.Symbol),
                    HydraUtils.SanitiseFeedId(gap.Key.Timeframe),
                    chunkStart);
                break;
            }

            if (batch.Count == 0) break;

            inserted   += await repository.UpsertBatchAsync(batch, ct).ConfigureAwait(false);
            chunkStart  = batch[^1].OpenTime.AddSeconds(
                HydraUtils.TimeframeToSeconds(gap.Key.Timeframe));
        }

        sw.Stop();

        _logger.LogInformation(
            "BackfillPipeline: filled [{Exchange}/{Symbol}/{Timeframe}] " +
            "inserted={Inserted} duration={Duration}ms",
            HydraUtils.SanitiseFeedId(gap.Key.Exchange),
            HydraUtils.SanitiseFeedId(gap.Key.Symbol),
            HydraUtils.SanitiseFeedId(gap.Key.Timeframe),
            inserted, sw.ElapsedMilliseconds);

        // Emit DATA_GAP_FILLED envelope
        var payload = new DataGapFilledPayload(
            Exchange:        gap.Key.Exchange,
            Symbol:          gap.Key.Symbol,
            Timeframe:       gap.Key.Timeframe,
            GapStart:        gap.GapStart,
            GapEnd:          gap.GapEnd,
            CandlesInserted: inserted,
            DurationMs:      sw.ElapsedMilliseconds);

        var envelope = EnvelopePayload.Create(
            MessageTypes.DataGapFilled, ModuleId, payload);

        await _envelopeSender.SendEnvelopeAsync(envelope, ct).ConfigureAwait(false);
    }

    // ── Per-exchange REST fetch ────────────────────────────────────────────────

    private Task<IReadOnlyList<CandleEntity>> FetchChunkAsync(
        FeedKey key, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct)
        => key.Exchange.ToLowerInvariant() switch
        {
            "hyperliquid" => FetchHyperliquidChunkAsync(key, from, to, limit, ct),
            "camelot"     => FetchCamelotChunkAsync(key, from, to, limit, ct),
            _             => Task.FromResult<IReadOnlyList<CandleEntity>>([])
        };

    private async Task<IReadOnlyList<CandleEntity>> FetchHyperliquidChunkAsync(
        FeedKey key, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct)
    {
        var coin     = HydraUtils.DeriveHyperliquidCoin(key.Symbol);
        var interval = HydraUtils.NormaliseHyperliquidInterval(key.Timeframe);

        var body = JsonSerializer.Serialize(new
        {
            type = "candleSnapshot",
            req  = new
            {
                coin,
                interval,
                startTime = from.ToUnixTimeMilliseconds(),
                endTime   = to.ToUnixTimeMilliseconds(),
            }
        });

        using var http     = _httpFactory.CreateClient("backfill");
        using var content  = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await http
            .PostAsync($"{_options.Value.HyperliquidRestUrl}/info", content, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return [];

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc          = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                                                    .ConfigureAwait(false);

        return ParseHyperliquidCandles(doc.RootElement, key, limit);
    }

    /// <summary>
    /// Parses the HYPERLIQUID <c>candleSnapshot</c> REST response.
    /// The response is a JSON array where each element may be either:
    /// <list type="bullet">
    ///   <item>An <b>object</b>: <c>{ "t":ms, "T":ms, "s":"BTC", "i":"1m", "o":"…", "c":"…", "h":"…", "l":"…", "v":"…", "n":… }</c></item>
    ///   <item>An <b>array</b> (positional): <c>[t, T, s, i, o, c, h, l, v, n]</c></item>
    /// </list>
    /// Both forms are handled.
    /// </summary>
    private static IReadOnlyList<CandleEntity> ParseHyperliquidCandles(
        JsonElement root, FeedKey key, int limit)
    {
        var result = new List<CandleEntity>();
        if (root.ValueKind != JsonValueKind.Array) return result;

        foreach (var item in root.EnumerateArray())
        {
            if (result.Count >= limit) break;

            long tsMs;
            double open, close, high, low, vol;

            if (item.ValueKind == JsonValueKind.Object)
            {
                if (!item.TryGetProperty("t", out var tp) || !tp.TryGetInt64(out tsMs)) continue;

                open  = HydraUtils.GetJsonDouble(item, "o");
                close = HydraUtils.GetJsonDouble(item, "c");
                high  = HydraUtils.GetJsonDouble(item, "h");
                low   = HydraUtils.GetJsonDouble(item, "l");
                vol   = HydraUtils.GetJsonDouble(item, "v");
            }
            else if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 8)
            {
                // Positional array: [t, T, s, i, o, c, h, l, v, n]
                //                    0  1  2  3  4  5  6  7  8  9
                if (!item[0].TryGetInt64(out tsMs)) continue;

                open  = HydraUtils.ParseJsonDouble(item[4]);
                close = HydraUtils.ParseJsonDouble(item[5]);
                high  = HydraUtils.ParseJsonDouble(item[6]);
                low   = HydraUtils.ParseJsonDouble(item[7]);
                vol   = HydraUtils.ParseJsonDouble(item[8]);
            }
            else
            {
                continue;
            }

            result.Add(new CandleEntity
            {
                Exchange    = key.Exchange,
                Symbol      = key.Symbol,
                Timeframe   = key.Timeframe,
                OpenTime    = DateTimeOffset.FromUnixTimeMilliseconds(tsMs),
                Open        = open,
                High        = high,
                Low         = low,
                Close       = close,
                Volume      = vol,
                QuoteVolume = vol * close,
                InsertedAt  = DateTimeOffset.UtcNow,
            });
        }

        return result;
    }

    private async Task<IReadOnlyList<CandleEntity>> FetchCamelotChunkAsync(
        FeedKey key, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct)
    {
        // Only 1h (poolHourDatas) and 1d (poolDayDatas) are available in the Camelot subgraph.
        if (!CamelotFeedCollector.SupportedTimeframes.Contains(key.Timeframe))
        {
            _logger.LogWarning(
                "BackfillPipeline: Camelot does not support timeframe '{Timeframe}' — skipping backfill",
                HydraUtils.SanitiseFeedId(key.Timeframe));
            return [];
        }

        const string SubgraphUrl =
            "https://api.thegraph.com/subgraphs/name/camelot-dex/camelot-v3";

        var entityName  = key.Timeframe == "1d" ? "poolDayDatas" : "poolHourDatas";
        var periodStart = from.ToUnixTimeSeconds();
        var periodEnd   = to.ToUnixTimeSeconds();

        // Sanitise symbol before embedding in the GraphQL query
        var rawSymbol = key.Symbol.ToLowerInvariant().Replace("-", "").Replace("/", "");
        var poolId    = HydraUtils.SanitiseFeedId(rawSymbol);

        var queryBody = new
        {
            query = $"{{ {entityName}(first: {limit}, orderBy: periodStartUnix, orderDirection: asc," +
                    $" where: {{ pool_: {{ token0_: {{symbol_contains_nocase: \"{poolId}\" }} }}," +
                    $" periodStartUnix_gte: {periodStart}, periodStartUnix_lt: {periodEnd} }}) {{" +
                    $" periodStartUnix high low open close volumeToken0 volumeToken1 }} }}"
        };
        var queryJson = JsonSerializer.Serialize(queryBody);

        using var http     = _httpFactory.CreateClient("backfill");
        using var content  = new StringContent(queryJson, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(SubgraphUrl, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return [];

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc          = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                                                    .ConfigureAwait(false);

        var result = new List<CandleEntity>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return result;
        if (!data.TryGetProperty(entityName, out var rows)) return result;

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("periodStartUnix", out var tsProp)
                || !tsProp.TryGetInt64(out var tsUnix)) continue;

            var open  = HydraUtils.GetJsonDouble(row, "open");
            var close = HydraUtils.GetJsonDouble(row, "close");
            var vol   = HydraUtils.GetJsonDouble(row, "volumeToken0");

            result.Add(new CandleEntity
            {
                Exchange    = key.Exchange,
                Symbol      = key.Symbol,
                Timeframe   = key.Timeframe,
                OpenTime    = DateTimeOffset.FromUnixTimeSeconds(tsUnix),
                Open        = open,
                High        = HydraUtils.GetJsonDouble(row, "high"),
                Low         = HydraUtils.GetJsonDouble(row, "low"),
                Close       = close,
                Volume      = vol,
                QuoteVolume = HydraUtils.GetJsonDouble(row, "volumeToken1"),
                InsertedAt  = DateTimeOffset.UtcNow,
            });
        }

        return result;
    }
}
