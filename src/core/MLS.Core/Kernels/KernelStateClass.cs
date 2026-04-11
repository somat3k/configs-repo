namespace MLS.Core.Kernels;

/// <summary>
/// High-level behavior class used for policy and certification.
/// </summary>
public enum KernelStateClass
{
    /// <summary>Deterministic and stateless across invocations.</summary>
    Pure = 0,

    /// <summary>Maintains bounded state between invocations.</summary>
    Stateful = 1,

    /// <summary>Produces progressive partial results.</summary>
    Streaming = 2,

    /// <summary>Transforms data representations and contracts.</summary>
    Transformational = 3,

    /// <summary>Coordinates child kernels behind an outer contract.</summary>
    Composite = 4,

    /// <summary>Runs training-oriented workloads with progress semantics.</summary>
    Training = 5,
}
