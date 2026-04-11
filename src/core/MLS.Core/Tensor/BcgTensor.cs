using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLS.Core.Tensor;

/// <summary>
/// Canonical high-value data carrier used by the BCG platform for governed execution.
/// Every BCG tensor is traceable, typed, shape-aware, and carries sufficient metadata
/// to support routing, validation, caching, persistence, replay, transformation,
/// compatibility checks, provenance reconstruction, and performance attribution.
/// </summary>
/// <remarks>
/// Identity rules:
/// <list type="bullet">
///   <item><see cref="Id"/> is globally unique and immutable once published.</item>
///   <item>Any material transformation (reshape, cast, slice, broadcast, semantic reinterpretation)
///   creates a new <see cref="BcgTensor"/> with lineage back to the prior one.</item>
///   <item><see cref="Meta"/>.<c>TraceId</c> must remain stable across a request/session lineage.</item>
///   <item><see cref="Meta"/>.<c>OriginModuleId</c> must be explicit for every production tensor.</item>
/// </list>
///
/// Encoding convention for raw byte dtypes:
/// When <see cref="Encoding"/> is <see cref="TensorEncoding.RawFloat32LE"/> or any other raw binary
/// encoding, the bytes are represented as a base64-encoded JSON string inside <see cref="Data"/>.
/// Consumers must base64-decode the string value before interpreting the bytes.
/// </remarks>
/// <param name="Id">Globally unique tensor identifier. Generated with <see cref="Guid.NewGuid"/>.</param>
/// <param name="DType">Declared scalar or semantic data type.</param>
/// <param name="Shape">
/// Declared tensor dimensions in row-major order. Use <c>-1</c> for dynamic dimensions
/// where the module contract permits them. Empty array denotes a scalar.
/// </param>
/// <param name="Layout">Physical memory layout of the payload.</param>
/// <param name="ShapeClass">Shape tolerance class declared by the producing module or kernel.</param>
/// <param name="TransportClass">Transport class selected by the routing governor.</param>
/// <param name="Encoding">Serialisation encoding of the inline <see cref="Data"/> payload.</param>
/// <param name="Data">
/// Inline data payload when <see cref="TransportClass"/> is <see cref="TensorTransportClass.Inline"/>.
/// <see langword="null"/> when the payload is externalized — see <see cref="Persistence"/>.
/// The <see cref="JsonElement"/> is always a self-contained clone; it is not tied to any
/// particular <see cref="JsonDocument"/> lifetime.
/// </param>
/// <param name="Meta">Origin and context metadata.</param>
/// <param name="Lineage">
/// Ordered history of transformation steps that produced this tensor from its ancestors.
/// Empty for root tensors born at an ingestion boundary.
/// </param>
/// <param name="Persistence">Storage reference when the payload is cached or externalized.</param>
/// <param name="Integrity">Integrity proofs for the payload and schema.</param>
public sealed record BcgTensor(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("dtype")] TensorDType DType,
    [property: JsonPropertyName("shape")] IReadOnlyList<int> Shape,
    [property: JsonPropertyName("layout")] TensorLayout Layout,
    [property: JsonPropertyName("shape_class")] TensorShapeClass ShapeClass,
    [property: JsonPropertyName("transport_class")] TensorTransportClass TransportClass,
    [property: JsonPropertyName("encoding")] TensorEncoding Encoding,
    [property: JsonPropertyName("data")] JsonElement? Data,
    [property: JsonPropertyName("meta")] TensorMeta Meta,
    [property: JsonPropertyName("lineage")] IReadOnlyList<TensorLineageRecord> Lineage,
    [property: JsonPropertyName("persistence")] TensorPersistenceRef? Persistence,
    [property: JsonPropertyName("integrity")] TensorIntegrity? Integrity)
{
    /// <summary>
    /// Creates a root (un-derived) inline tensor born at a block kernel or ingestion boundary.
    /// The <paramref name="data"/> element is cloned so the tensor is safe to route and persist
    /// beyond the lifetime of the caller's <see cref="JsonDocument"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="originModuleId"/> is null or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="shape"/> is null.
    /// </exception>
    public static BcgTensor CreateRoot(
        TensorDType dtype,
        IReadOnlyList<int> shape,
        TensorLayout layout,
        TensorShapeClass shapeClass,
        JsonElement data,
        TensorEncoding encoding,
        string originModuleId,
        Guid traceId,
        IReadOnlyList<string>? tags = null,
        Guid? originBlockId = null,
        Guid? sessionId = null,
        string? semanticTag = null,
        double? confidenceScore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originModuleId);
        ArgumentNullException.ThrowIfNull(shape);

        return new BcgTensor(
            Id: Guid.NewGuid(),
            DType: dtype,
            Shape: shape,
            Layout: layout,
            ShapeClass: shapeClass,
            TransportClass: TensorTransportClass.Inline,
            Encoding: encoding,
            Data: data.Clone(),
            Meta: new TensorMeta(
                OriginBlockId: originBlockId,
                OriginModuleId: originModuleId,
                TraceId: traceId,
                SessionId: sessionId,
                CreatedAt: DateTimeOffset.UtcNow,
                Tags: tags ?? [],
                ContractVersion: TensorMeta.CurrentContractVersion,
                SemanticTag: semanticTag,
                ConfidenceScore: confidenceScore),
            Lineage: [],
            Persistence: null,
            Integrity: null);
    }

    /// <summary>
    /// Creates a root reference tensor whose payload is externalized to a storage tier.
    /// Use this when the payload exceeds the inline size threshold.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="originModuleId"/> is null or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="shape"/> or <paramref name="persistence"/> is null.
    /// </exception>
    public static BcgTensor CreateReference(
        TensorDType dtype,
        IReadOnlyList<int> shape,
        TensorLayout layout,
        TensorShapeClass shapeClass,
        TensorEncoding encoding,
        string originModuleId,
        Guid traceId,
        TensorPersistenceRef persistence,
        TensorIntegrity? integrity = null,
        IReadOnlyList<string>? tags = null,
        Guid? originBlockId = null,
        Guid? sessionId = null,
        string? semanticTag = null,
        double? confidenceScore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originModuleId);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(persistence);

        return new BcgTensor(
            Id: Guid.NewGuid(),
            DType: dtype,
            Shape: shape,
            Layout: layout,
            ShapeClass: shapeClass,
            TransportClass: TensorTransportClass.Reference,
            Encoding: encoding,
            Data: null,
            Meta: new TensorMeta(
                OriginBlockId: originBlockId,
                OriginModuleId: originModuleId,
                TraceId: traceId,
                SessionId: sessionId,
                CreatedAt: DateTimeOffset.UtcNow,
                Tags: tags ?? [],
                ContractVersion: TensorMeta.CurrentContractVersion,
                SemanticTag: semanticTag,
                ConfidenceScore: confidenceScore),
            Lineage: [],
            Persistence: persistence,
            Integrity: integrity);
    }

    /// <summary>
    /// Creates an inline derived tensor that inherits the trace ID and appends a lineage step.
    /// The <paramref name="data"/> element is cloned so the tensor is safe to route and persist
    /// beyond the lifetime of the caller's <see cref="JsonDocument"/>.
    /// </summary>
    public BcgTensor Derive(
        TensorDType dtype,
        IReadOnlyList<int> shape,
        TensorLayout layout,
        TensorShapeClass shapeClass,
        JsonElement data,
        TensorEncoding encoding,
        TensorLineageRecord lineageStep,
        IReadOnlyList<string>? tags = null,
        string? semanticTag = null,
        double? confidenceScore = null)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(lineageStep);

        var newLineage = new List<TensorLineageRecord>(Lineage) { lineageStep };
        return new BcgTensor(
            Id: Guid.NewGuid(),
            DType: dtype,
            Shape: shape,
            Layout: layout,
            ShapeClass: shapeClass,
            TransportClass: TensorTransportClass.Inline,
            Encoding: encoding,
            Data: data.Clone(),
            Meta: new TensorMeta(
                OriginBlockId: lineageStep.ProducingBlockId,
                OriginModuleId: lineageStep.ProducingModuleId,
                TraceId: Meta.TraceId,
                SessionId: Meta.SessionId,
                CreatedAt: DateTimeOffset.UtcNow,
                Tags: tags ?? Meta.Tags,
                ContractVersion: TensorMeta.CurrentContractVersion,
                SemanticTag: semanticTag ?? Meta.SemanticTag,
                ConfidenceScore: confidenceScore),
            Lineage: newLineage,
            Persistence: null,
            Integrity: null);
    }

    /// <summary>
    /// Creates a reference-transport derived tensor. The payload is externalized; <see cref="Data"/> is null.
    /// Use when a transformation produces a large output that must be stored before dispatch.
    /// </summary>
    public BcgTensor DeriveReference(
        TensorDType dtype,
        IReadOnlyList<int> shape,
        TensorLayout layout,
        TensorShapeClass shapeClass,
        TensorEncoding encoding,
        TensorLineageRecord lineageStep,
        TensorPersistenceRef persistence,
        TensorIntegrity? integrity = null,
        IReadOnlyList<string>? tags = null,
        string? semanticTag = null,
        double? confidenceScore = null)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(lineageStep);
        ArgumentNullException.ThrowIfNull(persistence);

        var newLineage = new List<TensorLineageRecord>(Lineage) { lineageStep };
        return new BcgTensor(
            Id: Guid.NewGuid(),
            DType: dtype,
            Shape: shape,
            Layout: layout,
            ShapeClass: shapeClass,
            TransportClass: TensorTransportClass.Reference,
            Encoding: encoding,
            Data: null,
            Meta: new TensorMeta(
                OriginBlockId: lineageStep.ProducingBlockId,
                OriginModuleId: lineageStep.ProducingModuleId,
                TraceId: Meta.TraceId,
                SessionId: Meta.SessionId,
                CreatedAt: DateTimeOffset.UtcNow,
                Tags: tags ?? Meta.Tags,
                ContractVersion: TensorMeta.CurrentContractVersion,
                SemanticTag: semanticTag ?? Meta.SemanticTag,
                ConfidenceScore: confidenceScore),
            Lineage: newLineage,
            Persistence: persistence,
            Integrity: integrity);
    }

    /// <summary>
    /// Returns the element count implied by <see cref="Shape"/> for dense tensors,
    /// or <c>-1</c> when the shape contains dynamic dimensions or the count is not
    /// representable in a <see cref="long"/>.
    /// </summary>
    [JsonIgnore]
    public long ElementCount
    {
        get
        {
            if (Shape.Count == 0) return 1L;
            long count = 1L;
            try
            {
                foreach (var dim in Shape)
                {
                    if (dim < 0) return -1L; // dynamic dimension — unknown at inspection time
                    count = checked(count * dim);
                }
                return count;
            }
            catch (OverflowException)
            {
                return -1L; // element count exceeds the representable range for long
            }
        }
    }

    /// <summary>Returns <see langword="true"/> when the tensor is a root tensor (no ancestors).</summary>
    [JsonIgnore]
    public bool IsRoot => Lineage.Count == 0;
}
