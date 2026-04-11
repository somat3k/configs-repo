namespace MLS.Core.Tensor;

/// <summary>
/// Contract interface for modules and block kernels that produce or consume BCG tensors.
/// Every tensor-certified module must declare its accepted dtype families, shape classes,
/// and layout preferences so the Block Controller routing governor can make compatibility
/// decisions without interrogating the module at runtime.
/// </summary>
public interface ITensorContract
{
    // ── Identity ─────────────────────────────────────────────────────────────────

    /// <summary>Registered module or block identifier that owns this contract declaration.</summary>
    string ModuleId { get; }

    /// <summary>Certification level achieved for this module.</summary>
    TensorCertificationLevel CertificationLevel { get; }

    // ── Accepted inputs ───────────────────────────────────────────────────────────

    /// <summary>
    /// Set of dtype families this module accepts on its input boundary.
    /// An empty set means no tensor inputs are accepted (producer-only module).
    /// </summary>
    IReadOnlySet<TensorDType> AcceptedInputDTypes { get; }

    /// <summary>
    /// Shape tolerance classes this module accepts on its input boundary.
    /// The routing governor will attempt transformation if the incoming tensor's
    /// shape class is not in this set.
    /// </summary>
    IReadOnlySet<TensorShapeClass> AcceptedInputShapeClasses { get; }

    /// <summary>Layouts accepted on the input boundary.</summary>
    IReadOnlySet<TensorLayout> AcceptedInputLayouts { get; }

    // ── Produced outputs ──────────────────────────────────────────────────────────

    /// <summary>
    /// Set of dtype families this module emits on its output boundary.
    /// An empty set means no tensor outputs are produced (consumer-only module).
    /// </summary>
    IReadOnlySet<TensorDType> EmittedOutputDTypes { get; }

    /// <summary>Shape classes that apply to emitted output tensors.</summary>
    IReadOnlySet<TensorShapeClass> EmittedOutputShapeClasses { get; }

    // ── Compatibility check ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when the given tensor is compatible with this contract
    /// without requiring transformation bus intervention.
    /// </summary>
    bool IsCompatible(BcgTensor tensor);

    /// <summary>
    /// Returns the reason why the tensor is incompatible, or <see langword="null"/> when compatible.
    /// Used by the routing governor to construct typed compatibility error payloads.
    /// </summary>
    string? GetIncompatibilityReason(BcgTensor tensor);
}
