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
    private readonly HttpClient _http;
    private readonly IOptions<DataLayerOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnvelopeSender _envelopeSender;
    private readonly ILogger<BackfillPipeline> _logger;

    /// <summary>Initialises a new <see cref="BackfillPipeline"/>.</summary>
    public BackfillPipeline(
        HttpClient http,
        IOptions<DataLayerOptions> options,
        IServiceScopeFactory scopeFactory,
        IEnvelopeSender envelopeSender,
        ILogger<BackfillPipeline> logger)
    {
        _http           = http;
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
        _logger.LogDebug("BackfillPipeline: enqueued gap [{Exchange}/{Symbol}/{Timeframe}] {Start}→{End}",
            gap.Key.Exchange, gap.Key.Symbol, gap.Key.Timeframe, gap.GapStart, gap.GapEnd);
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
                    gap.Key.Exchange, gap.Key.Symbol, gap.Key.Timeframe);
            }
        }
    }

    // ── Core backfill logic ───────────────────────────────────────────────────

    private async Task ProcessGapAsync(GapRange gap, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "BackfillPipeline: filling [{Exchange}/{Symbol}/{Timeframe}] {Start}→{End} (~{Missing} candles)",
            gap.Key.Exchange, gap.Key.Symbol, gap.Key.Timeframe,
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
                    gap.Key.Exchange, gap.Key.Symbol, gap.Key.Timeframe, chunkStart);
                break;
            }

            if (batch.Count == 0) break;

            inserted   += await repository.UpsertBatchAsync(batch, ct).ConfigureAwait(false);
            chunkStart  = batch[^1].OpenTime.AddSeconds(
                GapDetector.TimeframeToSeconds(gap.Key.Timeframe));
        }

        sw.Stop();

        _logger.LogInformation(
            "BackfillPipeline: filled [{Exchange}/{Symbol}/{Timeframe}] " +
            "inserted={Inserted} duration={Duration}ms",
            gap.Key.Exchange, gap.Key.Symbol, gap.Key.Timeframe,
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
        var coin     = DeriveCoin(key.Symbol);
        var interval = NormaliseHlInterval(key.Timeframe);

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

        using var content  = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http
            .PostAsync($"{_options.Value.HyperliquidRestUrl}/info", content, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return [];

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc          = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                                                    .ConfigureAwait(false);

        return ParseHyperliquidCandles(doc.RootElement, key, limit);
    }

    private static IReadOnlyList<CandleEntity> ParseHyperliquidCandles(
        JsonElement root, FeedKey key, int limit)
    {
        var result = new List<CandleEntity>();
        if (root.ValueKind != JsonValueKind.Array) return result;

        foreach (var item in root.EnumerateArray())
        {
            if (result.Count >= limit) break;

            // Hyperliquid candleSnapshot: [t, T, s, i, o, c, h, l, v, n]
            // or object form: { "t":..., "T":..., "s":..., "i":..., "o":..., "c":..., "h":..., "l":..., "v":..., "n":... }
            static double Dp(JsonElement el, string k)
                => el.TryGetProperty(k, out var v)
                   && double.TryParse(v.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0.0;

            if (!item.TryGetProperty("t", out var tp) || !tp.TryGetInt64(out var tsMs)) continue;

            var open  = Dp(item, "o");
            var close = Dp(item, "c");
            var high  = Dp(item, "h");
            var low   = Dp(item, "l");
            var vol   = Dp(item, "v");

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
        // Reuse CamelotFeedCollector subgraph logic — simplified inline version
        const string SubgraphUrl =
            "https://api.thegraph.com/subgraphs/name/camelot-dex/camelot-v3";

        var periodStart = from.ToUnixTimeSeconds();
        var periodEnd   = to.ToUnixTimeSeconds();
        var poolId      = key.Symbol.ToLowerInvariant().Replace("-", "").Replace("/", "");

        var query = $$"""
            {
              "query": "{ poolHourDatas(first: {{limit}}, orderBy: periodStartUnix, orderDirection: asc,
                where: { pool_: { token0_: {symbol_contains_nocase: \"{{poolId}}\" } }, periodStartUnix_gte: {{periodStart}}, periodStartUnix_lt: {{periodEnd}} }) {
                  periodStartUnix high low open close volumeToken0 volumeToken1
              } }"
            }
            """;

        using var content  = new StringContent(query, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(SubgraphUrl, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return [];

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc          = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                                                    .ConfigureAwait(false);

        var result = new List<CandleEntity>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return result;
        if (!data.TryGetProperty("poolHourDatas", out var rows)) return result;

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("periodStartUnix", out var tsProp)
                || !tsProp.TryGetInt64(out var tsUnix)) continue;

            static double Dp(JsonElement el, string k)
                => el.TryGetProperty(k, out var v)
                   && double.TryParse(v.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0.0;

            var open  = Dp(row, "open");
            var close = Dp(row, "close");
            var vol   = Dp(row, "volumeToken0");

            result.Add(new CandleEntity
            {
                Exchange    = key.Exchange,
                Symbol      = key.Symbol,
                Timeframe   = key.Timeframe,
                OpenTime    = DateTimeOffset.FromUnixTimeSeconds(tsUnix),
                Open        = open,
                High        = Dp(row, "high"),
                Low         = Dp(row, "low"),
                Close       = close,
                Volume      = vol,
                QuoteVolume = Dp(row, "volumeToken1"),
                InsertedAt  = DateTimeOffset.UtcNow,
            });
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string DeriveCoin(string symbol)
    {
        var base_ = symbol.Split('-', '/')[0].ToUpperInvariant();
        return base_.Length > 1 && base_[0] == 'W' ? base_[1..] : base_;
    }

    private static string NormaliseHlInterval(string tf) => tf switch
    {
        "1m"  => "1m",  "3m"  => "3m",  "5m"  => "5m",
        "15m" => "15m", "30m" => "30m", "1h"  => "1h",
        "2h"  => "2h",  "4h"  => "4h",  "8h"  => "8h",
        "1d"  => "1d",  "1w"  => "1w",  _     => tf
    };
}
