using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>DEFI_HEALTH_WARNING</c> — emitted by the DeFi module when a position's
/// health factor approaches or crosses the liquidation threshold.
/// </summary>
/// <param name="PositionId">Unique identifier of the at-risk position.</param>
/// <param name="Protocol">Protocol name (e.g. <c>"morpho"</c>, <c>"balancer"</c>).</param>
/// <param name="Asset">Borrowed asset symbol.</param>
/// <param name="Collateral">Collateral asset symbol.</param>
/// <param name="HealthFactor">Current health factor. Alert triggers at HF &lt; <paramref name="Threshold"/>.</param>
/// <param name="Threshold">Configured alert threshold (typically 1.2).</param>
/// <param name="Severity">Severity level: <c>Warning</c>, <c>Critical</c>.</param>
/// <param name="LiquidationPrice">Estimated price at which liquidation occurs.</param>
public sealed record DefiHealthWarningPayload(
    [property: JsonPropertyName("position_id")] Guid PositionId,
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("asset")] string Asset,
    [property: JsonPropertyName("collateral")] string Collateral,
    [property: JsonPropertyName("health_factor")] double HealthFactor,
    [property: JsonPropertyName("threshold")] double Threshold,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("liquidation_price")] decimal LiquidationPrice);
