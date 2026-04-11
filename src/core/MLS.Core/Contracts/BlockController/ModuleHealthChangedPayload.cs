using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.BlockController;

/// <summary>
/// Payload emitted when a module transitions to a new health state.
/// </summary>
/// <param name="ModuleId">ID of the affected module.</param>
/// <param name="ModuleName">Human-readable name of the module.</param>
/// <param name="PreviousState">Health state before the transition.</param>
/// <param name="CurrentState">Health state after the transition.</param>
/// <param name="Reason">Optional human-readable reason for the transition.</param>
/// <param name="Timestamp">UTC time of the transition.</param>
public sealed record ModuleHealthChangedPayload(
    [property: JsonPropertyName("module_id")] Guid ModuleId,
    [property: JsonPropertyName("module_name")] string ModuleName,
    [property: JsonPropertyName("previous_state")] string PreviousState,
    [property: JsonPropertyName("current_state")] string CurrentState,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
