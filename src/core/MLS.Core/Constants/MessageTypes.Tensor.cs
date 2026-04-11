namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    // ── Tensor lifecycle ─────────────────────────────────────────────────────────
    /// <summary>Block Controller → producer: tensor failed contract validation.</summary>
    public const string TensorValidationFailed     = "TENSOR_VALIDATION_FAILED";
    /// <summary>Block Controller → telemetry: tensor successfully routed to a target module.</summary>
    public const string TensorRouted               = "TENSOR_ROUTED";
    /// <summary>Transformation bus → block controller: tensor was materially transformed into a new identity.</summary>
    public const string TensorTransformed          = "TENSOR_TRANSFORMED";
    /// <summary>Storage service → block controller: tensor payload was persisted or relocated between tiers.</summary>
    public const string TensorStored               = "TENSOR_STORED";
    /// <summary>Any tensor-producing module → block controller: new lineage record created.</summary>
    public const string TensorLineageCreated       = "TENSOR_LINEAGE_CREATED";
    /// <summary>Block Controller → producer: tensor is incompatible with the target module and no legal transformation exists.</summary>
    public const string TensorCompatibilityError   = "TENSOR_COMPATIBILITY_ERROR";
    /// <summary>Tensor-producing module → block controller: tensor successfully validated and accepted into the fabric.</summary>
    public const string TensorAccepted             = "TENSOR_ACCEPTED";
    /// <summary>ML Runtime / TensorTrainer → block controller: batch of tensors completed inference or training pass.</summary>
    public const string TensorBatchComplete        = "TENSOR_BATCH_COMPLETE";
}
