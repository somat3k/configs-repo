using System.Text.Json.Serialization;
using MLS.Core.Tensor;

namespace MLS.Core.Contracts.Tensor;

/// <summary>
/// Payload for <c>TENSOR_STORED</c>.
/// Emitted when a tensor payload is persisted or relocated between storage tiers.
/// </summary>
/// <param name="TensorId">ID of the stored tensor.</param>
/// <param name="TraceId">Trace correlation ID.</param>
/// <param name="StorageMode">Storage tier where the payload was written.</param>
/// <param name="StorageKey">
/// Tier-specific storage key: Redis key, Postgres record ID string, or IPFS CID.
/// </param>
/// <param name="ExpiresAt">UTC expiry time for TTL-governed tiers, or <see langword="null"/> for permanent records.</param>
/// <param name="PayloadHash">SHA-256 hex digest of the stored payload for integrity verification.</param>
public sealed record TensorStoredPayload(
    [property: JsonPropertyName("tensor_id")] Guid TensorId,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("storage_mode")] TensorStorageMode StorageMode,
    [property: JsonPropertyName("storage_key")] string StorageKey,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("payload_hash")] string PayloadHash);
