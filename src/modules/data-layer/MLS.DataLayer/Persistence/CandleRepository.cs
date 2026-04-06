using Microsoft.EntityFrameworkCore;

namespace MLS.DataLayer.Persistence;

/// <summary>
/// Data-access helper for <see cref="CandleEntity"/> persisted in PostgreSQL.
/// </summary>
public sealed class CandleRepository(DataLayerDbContext _db)
{
    /// <summary>
    /// Upserts a batch of candles using <c>ON CONFLICT DO NOTHING</c> so that duplicate
    /// candles (same exchange/symbol/timeframe/open_time) are silently skipped.
    /// </summary>
    /// <returns>Number of rows actually inserted.</returns>
    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<CandleEntity> candles,
        CancellationToken ct = default)
    {
        if (candles.Count == 0) return 0;

        await _db.EnsureSchemaAsync(ct).ConfigureAwait(false);

        // ExecuteUpdate / raw SQL for efficient ON CONFLICT DO NOTHING
        var sql = """
            INSERT INTO candles
                (exchange, symbol, timeframe, open_time, open, high, low, close, volume, quote_volume, inserted_at)
            VALUES
            """;

        // Build parameterised query manually for batch insert
        var paramRows   = new List<string>(candles.Count);
        var parameters  = new List<object>(candles.Count * 11);
        int idx         = 0;

        foreach (var c in candles)
        {
            paramRows.Add(
                $"({{{idx}}},{{{idx+1}}},{{{idx+2}}},{{{idx+3}}},{{{idx+4}}},{{{idx+5}}},{{{idx+6}}},{{{idx+7}}},{{{idx+8}}},{{{idx+9}}},{{{idx+10}}})");
            parameters.Add(c.Exchange);
            parameters.Add(c.Symbol);
            parameters.Add(c.Timeframe);
            parameters.Add(c.OpenTime);
            parameters.Add(c.Open);
            parameters.Add(c.High);
            parameters.Add(c.Low);
            parameters.Add(c.Close);
            parameters.Add(c.Volume);
            parameters.Add(c.QuoteVolume);
            parameters.Add(DateTimeOffset.UtcNow);
            idx += 11;
        }

        var fullSql = sql + "\n" + string.Join(",\n", paramRows)
                    + "\nON CONFLICT (exchange, symbol, timeframe, open_time) DO NOTHING";

        return await _db.Database.ExecuteSqlRawAsync(fullSql, parameters, ct)
                        .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the latest stored <c>open_time</c> for the given feed tuple,
    /// or <see langword="null"/> if no candles have been stored yet.
    /// </summary>
    public async Task<DateTimeOffset?> GetLatestOpenTimeAsync(
        string exchange, string symbol, string timeframe,
        CancellationToken ct = default)
    {
        return await _db.Candles
            .Where(c => c.Exchange == exchange && c.Symbol == symbol && c.Timeframe == timeframe)
            .MaxAsync(c => (DateTimeOffset?)c.OpenTime, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the count of candles with <c>open_time &gt; <paramref name="since"/></c>
    /// for the given feed tuple.
    /// </summary>
    public async Task<int> CountSinceAsync(
        string exchange, string symbol, string timeframe,
        DateTimeOffset since,
        CancellationToken ct = default)
    {
        return await _db.Candles
            .Where(c => c.Exchange == exchange
                     && c.Symbol    == symbol
                     && c.Timeframe == timeframe
                     && c.OpenTime  > since)
            .CountAsync(ct)
            .ConfigureAwait(false);
    }
}

/// <summary>Internal EF Core migration helper.</summary>
internal static class DbContextExtensions
{
    private static int _schemaEnsured;

    /// <summary>
    /// Applies any pending EF Core migrations on first call; subsequent calls are no-ops.
    /// </summary>
    internal static async Task EnsureSchemaAsync(
        this DataLayerDbContext db,
        CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _schemaEnsured, 1) == 0)
            await db.Database.MigrateAsync(ct).ConfigureAwait(false);
    }
}
