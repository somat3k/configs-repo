namespace MLS.Core.Tensor;

/// <summary>
/// Physical memory layout of a <see cref="BcgTensor"/> payload.
/// The layout determines how the tensor may be routed, persisted, and consumed.
/// </summary>
public enum TensorLayout
{
    /// <summary>
    /// Contiguous row-major float buffer. Default for model inference and training.
    /// All dimensions are explicit and bounded.
    /// </summary>
    Dense,

    /// <summary>
    /// COO / CSR sparse format: indices + values arrays.
    /// Used for graph adjacency matrices and irregular scientific data.
    /// </summary>
    Sparse,

    /// <summary>
    /// Variable-length rows — each row may have a different length.
    /// Used for tokenised text, event sequences, code segments.
    /// </summary>
    Ragged,

    /// <summary>
    /// Ordered sequence of uniform elements with a time or position axis.
    /// Used for event sequences, sliding windows, temporal feature maps.
    /// </summary>
    Sequence,

    /// <summary>
    /// Tensor derived from a graph topology projection (neighbourhood, multi-hop).
    /// Shape may be dynamic but is bounded by the graph size.
    /// </summary>
    GraphDerived,

    /// <summary>
    /// Payload is externalized — the tensor carries only a reference (Redis key,
    /// Postgres record ID, or IPFS CID) rather than inline data.
    /// Consumers must fetch the artifact on demand.
    /// </summary>
    ArtifactBacked,
}
