using System.Text.Json.Serialization;

namespace MLS.Core.Tensor;

/// <summary>
/// Immutable reference to the externalized or persisted copy of a tensor payload.
/// At most one tier may hold the authoritative copy at any time.
/// </summary>
/// <param name="RedisKey">
/// Redis key under which the tensor payload is cached, or <see langword="null"/> if not cached.
/// </param>
/// <param name="PostgresRecordId">
/// Stable PostgreSQL record identifier in the tensor registry table,
/// or <see langword="null"/> if not yet registered.
/// </param>
/// <param name="IpfsCid">
/// IPFS Content Identifier for a large externalized payload,
/// or <see langword="null"/> if not externalized.
/// </param>
/// <param name="StorageMode">Current authoritative storage tier.</param>
/// <param name="ExpiresAt">
/// UTC time after which the persisted copy may be evicted or archived,
/// or <see langword="null"/> for non-expiring records.
/// </param>
public sealed record TensorPersistenceRef(
    [property: JsonPropertyName("redis_key")] string? RedisKey,
    [property: JsonPropertyName("postgres_record_id")] Guid? PostgresRecordId,
    [property: JsonPropertyName("ipfs_cid")] string? IpfsCid,
    [property: JsonPropertyName("storage_mode")] TensorStorageMode StorageMode,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt)
{
    /// <summary>Returns <see langword="true"/> when the tensor payload is externalized (not inline).</summary>
    [JsonIgnore]
    public bool IsExternalized =>
        StorageMode is TensorStorageMode.Redis or TensorStorageMode.Postgres or TensorStorageMode.Ipfs;
}
