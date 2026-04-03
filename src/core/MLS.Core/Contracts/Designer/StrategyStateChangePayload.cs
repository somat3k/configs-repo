using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>STRATEGY_STATE_CHANGE</c> — broadcast when a deployed strategy
/// transitions between <c>Running</c>, <c>Stopped</c>, and <c>Backtesting</c>.
/// </summary>
/// <param name="StrategyId">Identifier of the affected strategy graph.</param>
/// <param name="PreviousState">State before the transition.</param>
/// <param name="CurrentState">State after the transition.</param>
/// <param name="Timestamp">UTC time of the state change.</param>
public sealed record StrategyStateChangePayload(
    [property: JsonPropertyName("strategy_id")]    Guid StrategyId,
    [property: JsonPropertyName("previous_state")] string PreviousState,
    [property: JsonPropertyName("current_state")]  string CurrentState,
    [property: JsonPropertyName("timestamp")]      DateTimeOffset Timestamp);
