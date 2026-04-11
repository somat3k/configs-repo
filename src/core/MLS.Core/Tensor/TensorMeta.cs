using System.Text.Json.Serialization;

namespace MLS.Core.Tensor;

/// <summary>
/// Origin and context metadata carried by every <see cref="BcgTensor"/>.
/// All fields are immutable once the tensor is published.
/// </summary>
/// <param name="OriginBlockId">
/// Identifier of the block that produced this tensor, or <see langword="null"/> when
/// the tensor originates at the module boundary rather than inside a block graph.
/// </param>
/// <param name="OriginModuleId">
/// Registered module identifier of the producing service.
/// MUST be explicit for every production tensor.
/// </param>
/// <param name="TraceId">
/// Correlation identifier that remains stable across a request/session lineage.
/// A new root trace ID must be generated only when a new top-level process is intentionally created.
/// </param>
/// <param name="SessionId">Optional session identifier when the tensor belongs to a user session.</param>
/// <param name="CreatedAt">UTC timestamp of tensor creation.</param>
/// <param name="Tags">Free-form classification tags (e.g. "inference", "embedding", "training").</param>
/// <param name="ContractVersion">
/// Version of the universal tensor contract this tensor was produced under.
/// Used for forward-compatibility checks during validation.
/// </param>
/// <param name="SemanticTag">
/// Semantic sub-type hint for <see cref="TensorDType"/> values that share a physical encoding
/// (e.g. <see cref="TensorDType.Bytes"/> carrying a graph-serialised packet vs. a raw binary blob).
/// </param>
/// <param name="ConfidenceScore">
/// Optional quality or confidence score in the range [0, 1] provided by the producing kernel.
/// </param>
public sealed record TensorMeta(
    [property: JsonPropertyName("origin_block_id")] Guid? OriginBlockId,
    [property: JsonPropertyName("origin_module_id")] string OriginModuleId,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("session_id")] Guid? SessionId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("contract_version")] int ContractVersion,
    [property: JsonPropertyName("semantic_tag")] string? SemanticTag,
    [property: JsonPropertyName("confidence_score")] double? ConfidenceScore)
{
    /// <summary>Current contract version emitted by all producing modules.</summary>
    public const int CurrentContractVersion = 1;
}
