namespace MLS.Core.Tensor;

/// <summary>
/// Governs how a tensor payload moves between producer and consumer.
/// The routing governor selects the transport class based on payload size,
/// latency requirements, persistence policy, and lane suitability.
/// </summary>
public enum TensorTransportClass
{
    /// <summary>
    /// Tensor payload is embedded directly in the envelope.
    /// Used when the payload is small, latency is critical, and no
    /// externalization threshold is exceeded.
    /// Typical transports: gRPC request/response, WebSocket event, HTTP body.
    /// </summary>
    Inline,

    /// <summary>
    /// Tensor payload is delivered incrementally as a stream of partial outputs.
    /// Used when partial results are meaningful or when a long-running transform
    /// produces progressive output.
    /// Typical transports: gRPC streaming, SignalR/WebSocket streams, SSE.
    /// </summary>
    Stream,

    /// <summary>
    /// Tensor payload is externalized. The envelope carries only the metadata and
    /// a storage reference (Redis key, Postgres ID, or IPFS CID).
    /// Consumers fetch the artifact on demand (lazy fetch pattern).
    /// Used when the payload is large, multiple consumers share the same object,
    /// or replay/archival is required.
    /// </summary>
    Reference,
}
