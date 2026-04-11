using System.Text.Json.Serialization;

namespace MLS.Core.Tensor;

/// <summary>
/// Immutable reference to the externalized or persisted copy of a tensor payload.
/// Exactly one of <see cref="RedisKey"/>, <see cref="PostgresRecordId"/>, or <see cref="IpfsCid"/>
/// must be set for externalized storage modes; none may be set for <see cref="TensorStorageMode.Transient"/>.
/// </summary>
/// <param name="RedisKey">
/// Redis key under which the tensor payload is cached, or <see langword="null"/> if not cached.
/// Must be the only reference field set when <paramref name="StorageMode"/> is <see cref="TensorStorageMode.Redis"/>.
/// </param>
/// <param name="PostgresRecordId">
/// Stable PostgreSQL record identifier in the tensor registry table,
/// or <see langword="null"/> if not yet registered.
/// Must be the only reference field set when <paramref name="StorageMode"/> is <see cref="TensorStorageMode.Postgres"/>.
/// </param>
/// <param name="IpfsCid">
/// IPFS Content Identifier for a large externalized payload,
/// or <see langword="null"/> if not externalized.
/// Must be the only reference field set when <paramref name="StorageMode"/> is <see cref="TensorStorageMode.Ipfs"/>.
/// </param>
/// <param name="StorageMode">Current authoritative storage tier.</param>
/// <param name="ExpiresAt">
/// UTC time after which the persisted copy may be evicted or archived,
/// or <see langword="null"/> for non-expiring records.
/// </param>
/// <exception cref="ArgumentException">
/// Thrown when the combination of storage mode and reference fields is invalid.
/// </exception>
public sealed record TensorPersistenceRef(
    [property: JsonPropertyName("redis_key")] string? RedisKey,
    [property: JsonPropertyName("postgres_record_id")] Guid? PostgresRecordId,
    [property: JsonPropertyName("ipfs_cid")] string? IpfsCid,
    TensorStorageMode StorageMode,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt)
{
    /// <summary>Current authoritative storage tier. Validated against the reference fields at construction.</summary>
    [JsonPropertyName("storage_mode")]
    public TensorStorageMode StorageMode { get; init; } =
        ValidateStorageMode(StorageMode, RedisKey, PostgresRecordId, IpfsCid);

    private static TensorStorageMode ValidateStorageMode(
        TensorStorageMode mode,
        string? redisKey,
        Guid? postgresRecordId,
        string? ipfsCid)
    {
        var hasRedisKey = !string.IsNullOrWhiteSpace(redisKey);
        var hasPostgresRecordId = postgresRecordId.HasValue;
        var hasIpfsCid = !string.IsNullOrWhiteSpace(ipfsCid);

        switch (mode)
        {
            case TensorStorageMode.Redis:
                if (!hasRedisKey || hasPostgresRecordId || hasIpfsCid)
                    throw new ArgumentException(
                        "TensorPersistenceRef with Redis storage mode must specify only RedisKey.");
                break;

            case TensorStorageMode.Postgres:
                if (!hasPostgresRecordId || hasRedisKey || hasIpfsCid)
                    throw new ArgumentException(
                        "TensorPersistenceRef with Postgres storage mode must specify only PostgresRecordId.");
                break;

            case TensorStorageMode.Ipfs:
                if (!hasIpfsCid || hasRedisKey || hasPostgresRecordId)
                    throw new ArgumentException(
                        "TensorPersistenceRef with Ipfs storage mode must specify only IpfsCid.");
                break;

            default: // Transient
                if (hasRedisKey || hasPostgresRecordId || hasIpfsCid)
                    throw new ArgumentException(
                        "TensorPersistenceRef with a non-externalized storage mode must not specify external references.");
                break;
        }

        return mode;
    }

    /// <summary>Returns <see langword="true"/> when the tensor payload is externalized (not inline).</summary>
    [JsonIgnore]
    public bool IsExternalized => StorageMode switch
    {
        TensorStorageMode.Redis =>
            !string.IsNullOrWhiteSpace(RedisKey) &&
            !PostgresRecordId.HasValue &&
            string.IsNullOrWhiteSpace(IpfsCid),
        TensorStorageMode.Postgres =>
            string.IsNullOrWhiteSpace(RedisKey) &&
            PostgresRecordId.HasValue &&
            string.IsNullOrWhiteSpace(IpfsCid),
        TensorStorageMode.Ipfs =>
            string.IsNullOrWhiteSpace(RedisKey) &&
            !PostgresRecordId.HasValue &&
            !string.IsNullOrWhiteSpace(IpfsCid),
        _ => false,
    };
}
