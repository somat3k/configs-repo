using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MLS.DataLayer.Persistence;

namespace MLS.DataLayer.FeatureStore;

/// <summary>
/// Data-access helper for <see cref="FeatureStoreEntity"/> persisted in the
/// <c>feature_store_vectors</c> PostgreSQL table.
/// </summary>
public sealed class FeatureStoreRepository(DataLayerDbContext _db)
{
    // ── Upsert ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists a computed <see cref="FeatureVector"/> for a given feed and timestamp.
    /// Uses <c>ON CONFLICT DO UPDATE</c> so that re-computing features for the same bar
    /// updates the row in place.
    /// </summary>
    /// <param name="exchange">Exchange identifier, e.g. <c>hyperliquid</c>.</param>
    /// <param name="symbol">Trading symbol, e.g. <c>BTC-USDT</c>.</param>
    /// <param name="timeframe">Candle timeframe, e.g. <c>1h</c>.</param>
    /// <param name="featureTimestamp">
    /// UTC timestamp of the last candle in the window used to compute the vector.
    /// </param>
    /// <param name="vector">Computed feature vector.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of rows inserted or updated (always 1 on success).</returns>
    public async Task<int> UpsertAsync(
        string exchange,
        string symbol,
        string timeframe,
        DateTimeOffset featureTimestamp,
        FeatureVector vector,
        CancellationToken ct = default)
    {
        var featuresJson = JsonSerializer.Serialize(vector.ToArray());
        var modelType    = ModelTypeIds.For(vector.ModelType);
        var now          = DateTimeOffset.UtcNow;

        var sql = """
            INSERT INTO feature_store_vectors
                (exchange, symbol, timeframe, model_type, schema_version, feature_timestamp, features_json, computed_at)
            VALUES
                ({0},{1},{2},{3},{4},{5},{6},{7})
            ON CONFLICT (exchange, symbol, timeframe, model_type, feature_timestamp)
            DO UPDATE SET
                schema_version    = EXCLUDED.schema_version,
                features_json     = EXCLUDED.features_json,
                computed_at       = EXCLUDED.computed_at
            """;

        return await _db.Database.ExecuteSqlRawAsync(
                sql,
                [exchange, symbol, timeframe, modelType,
                 vector.SchemaVersion, featureTimestamp,
                 featuresJson, now],
                ct)
            .ConfigureAwait(false);
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the most-recently computed <see cref="FeatureStoreEntity"/> for the
    /// given feed tuple and model type, or <see langword="null"/> if none exists.
    /// </summary>
    public async Task<FeatureStoreEntity?> GetLatestAsync(
        string exchange,
        string symbol,
        string timeframe,
        ModelType modelType,
        CancellationToken ct = default)
    {
        var mt = ModelTypeIds.For(modelType);
        return await _db.FeatureStore
            .Where(f => f.Exchange   == exchange
                     && f.Symbol     == symbol
                     && f.Timeframe  == timeframe
                     && f.ModelType  == mt)
            .OrderByDescending(f => f.FeatureTimestamp)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns all <see cref="FeatureStoreEntity"/> rows for the given feed tuple
    /// and model type in the specified time range, ordered oldest-first.
    /// </summary>
    public async Task<IReadOnlyList<FeatureStoreEntity>> GetRangeAsync(
        string exchange,
        string symbol,
        string timeframe,
        ModelType modelType,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var mt = ModelTypeIds.For(modelType);
        return await _db.FeatureStore
            .Where(f => f.Exchange          == exchange
                     && f.Symbol            == symbol
                     && f.Timeframe         == timeframe
                     && f.ModelType         == mt
                     && f.FeatureTimestamp  >= from
                     && f.FeatureTimestamp  <  to)
            .OrderBy(f => f.FeatureTimestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes feature rows older than <paramref name="before"/> for the given feed
    /// and model type. Useful for housekeeping in long-running deployments.
    /// </summary>
    /// <returns>Number of rows deleted.</returns>
    public async Task<int> PurgeOlderThanAsync(
        string exchange,
        string symbol,
        string timeframe,
        ModelType modelType,
        DateTimeOffset before,
        CancellationToken ct = default)
    {
        var mt = ModelTypeIds.For(modelType);
        return await _db.FeatureStore
            .Where(f => f.Exchange          == exchange
                     && f.Symbol            == symbol
                     && f.Timeframe         == timeframe
                     && f.ModelType         == mt
                     && f.FeatureTimestamp  <  before)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
