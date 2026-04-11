namespace MLS.Core.Kernels;

/// <summary>
/// Canonical lifecycle states for a production kernel instance.
/// </summary>
public enum KernelState
{
    /// <summary>Kernel metadata exists but no resources are prepared.</summary>
    Uninitialized = 0,

    /// <summary>Kernel initialization is in progress.</summary>
    Initializing = 1,

    /// <summary>Kernel is healthy and can accept work.</summary>
    Ready = 2,

    /// <summary>Kernel is actively processing a non-stream execution.</summary>
    Running = 3,

    /// <summary>Kernel is actively emitting streamed output fragments.</summary>
    Streaming = 4,

    /// <summary>Kernel is creating or applying checkpoint state.</summary>
    Checkpointing = 5,

    /// <summary>Kernel is draining and disposing resources.</summary>
    Disposing = 6,

    /// <summary>Kernel has entered a terminal failure state.</summary>
    Faulted = 7,
}
