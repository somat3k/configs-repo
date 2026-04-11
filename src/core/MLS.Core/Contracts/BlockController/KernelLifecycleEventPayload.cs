using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.BlockController;

/// <summary>
/// Structured kernel lifecycle event payload emitted by the Block Controller.
/// </summary>
/// <param name="BlockId">Logical block identifier associated with the kernel operation.</param>
/// <param name="KernelOperationId">Resolved kernel operation identity.</param>
/// <param name="TraceId">Trace identifier for observability.</param>
/// <param name="ModuleId">Module selected for kernel execution.</param>
/// <param name="LaneId">Execution lane identifier.</param>
/// <param name="State">Kernel lifecycle state label.</param>
/// <param name="Reason">Optional transition reason.</param>
/// <param name="Timestamp">UTC timestamp.</param>
public sealed record KernelLifecycleEventPayload(
    [property: JsonPropertyName("block_id")] Guid BlockId,
    [property: JsonPropertyName("kernel_operation_id")] string KernelOperationId,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("module_id")] string ModuleId,
    [property: JsonPropertyName("lane_id")] string LaneId,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
