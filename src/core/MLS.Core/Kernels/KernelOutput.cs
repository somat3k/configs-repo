using MLS.Core.Tensor;

namespace MLS.Core.Kernels;

/// <summary>
/// Typed kernel output fragment.
/// </summary>
/// <param name="Data">Output tensor payload.</param>
/// <param name="IsFinal">Whether this output marks the end of execution/stream.</param>
/// <param name="FragmentIndex">Zero-based output fragment index.</param>
/// <param name="TraceId">Trace identifier associated with this output.</param>
public sealed record KernelOutput(
    BcgTensor Data,
    bool IsFinal,
    int FragmentIndex,
    Guid TraceId);
