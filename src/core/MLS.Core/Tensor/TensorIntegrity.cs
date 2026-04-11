using System.Text.Json.Serialization;

namespace MLS.Core.Tensor;

/// <summary>
/// Integrity proofs attached to a <see cref="BcgTensor"/> at publication time.
/// Consumers may use these to detect data corruption, verify provenance,
/// and avoid redundant full-payload rescans.
/// </summary>
/// <param name="PayloadHash">
/// SHA-256 hex digest of the serialised payload bytes.
/// Large-payload validation MUST use this fingerprint rather than a full-copy rescan.
/// </param>
/// <param name="ContractFingerprint">
/// Hash of the tensor contract schema version and field layout used during production.
/// Allows fast schema compatibility checks without a full field inspection.
/// </param>
/// <param name="Signature">
/// Optional HMAC or asymmetric signature for trusted artifact verification.
/// Required when the tensor originates from an external ingestion path.
/// </param>
public sealed record TensorIntegrity(
    [property: JsonPropertyName("payload_hash")] string PayloadHash,
    [property: JsonPropertyName("contract_fingerprint")] string ContractFingerprint,
    [property: JsonPropertyName("signature")] string? Signature);
