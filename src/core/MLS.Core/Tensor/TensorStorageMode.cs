namespace MLS.Core.Tensor;

/// <summary>
/// Persistence tier that currently holds the tensor payload or its authoritative reference.
/// Storage routing is governed by tensor size, access frequency, and TTL policy.
/// </summary>
public enum TensorStorageMode
{
    /// <summary>
    /// Tensor is held only in-process memory. No external persistence.
    /// The payload is lost when the producing context is garbage-collected.
    /// Allowed only for local working state before publication.
    /// </summary>
    Transient,

    /// <summary>
    /// Tensor is cached in Redis.
    /// Used for hot tensors, transient intermediate state, recent inference
    /// outputs, and short-lived stream windows.
    /// </summary>
    Redis,

    /// <summary>
    /// Tensor metadata and lineage are persisted in PostgreSQL.
    /// Used for the tensor registry, audit events, stable references,
    /// and control-plane persistence.
    /// </summary>
    Postgres,

    /// <summary>
    /// Large tensor payload is externalized to IPFS.
    /// Used for dataset snapshots, training artifacts, batch archives,
    /// and large model inputs that exceed the inline size threshold.
    /// </summary>
    Ipfs,
}
