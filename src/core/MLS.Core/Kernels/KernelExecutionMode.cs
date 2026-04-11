namespace MLS.Core.Kernels;

/// <summary>
/// Execution modes supported by a kernel descriptor.
/// </summary>
public enum KernelExecutionMode
{
    /// <summary>Low-latency synchronous execution path.</summary>
    Sync = 0,

    /// <summary>Asynchronous execution path.</summary>
    Async = 1,

    /// <summary>Batch-oriented throughput path.</summary>
    Batch = 2,

    /// <summary>Chained execution path in a pipeline.</summary>
    Pipeline = 3,

    /// <summary>Progressive streaming execution path.</summary>
    Streaming = 4,

    /// <summary>Long-running heavy compute path.</summary>
    HeavyCompute = 5,
}
