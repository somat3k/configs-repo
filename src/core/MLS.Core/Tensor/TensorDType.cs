namespace MLS.Core.Tensor;

/// <summary>
/// Canonical declared data type for a <see cref="BcgTensor"/>.
/// DType is part of execution legality — not merely a serialisation detail.
/// No hidden casting is permitted in production routes; any cast must be explicit,
/// observable, and lineage-preserving.
/// </summary>
public enum TensorDType
{
    // ── Primitive numeric ────────────────────────────────────────────────────────
    /// <summary>32-bit IEEE 754 single-precision float. Default for model I/O.</summary>
    Float32,
    /// <summary>64-bit IEEE 754 double-precision float. Used for scientific compute.</summary>
    Float64,
    /// <summary>Signed 32-bit integer.</summary>
    Int32,
    /// <summary>Signed 64-bit integer.</summary>
    Int64,
    /// <summary>Unsigned 8-bit integer. Used for quantised weights and image data.</summary>
    UInt8,
    /// <summary>Boolean flag tensor.</summary>
    Bool,

    // ── Text and bytes ───────────────────────────────────────────────────────────
    /// <summary>UTF-8 string payload. Semantic tag determines interpretation.</summary>
    String,
    /// <summary>Raw binary payload. Semantic tag determines interpretation.</summary>
    Bytes,

    // ── Semantic types ───────────────────────────────────────────────────────────
    /// <summary>
    /// JSON-structured semantic payload. Physically serialised as UTF-8 bytes,
    /// but carries JSON schema identity in the semantic tag.
    /// </summary>
    JsonSemantic,
    /// <summary>
    /// Dense float vector from an embedding model (text, image, code).
    /// Carries model identity and dimension count in the semantic tag.
    /// </summary>
    EmbeddingVector,
    /// <summary>
    /// Serialised code token stream (e.g. tokenised source, AST).
    /// Must preserve vocabulary identity in the semantic tag.
    /// </summary>
    CodeTokenStream,
    /// <summary>
    /// Serialised graph packet — adjacency list or incidence structure.
    /// Carries topology schema version in the semantic tag.
    /// </summary>
    GraphPacket,
}
