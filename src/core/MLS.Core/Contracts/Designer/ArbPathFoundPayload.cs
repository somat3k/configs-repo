using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>ARB_PATH_FOUND</c> — emitted by the arbitrage scanner when a profitable
/// multi-hop path is discovered. Sent to both Designer and Broker.
/// </summary>
/// <param name="PathId">Unique path identifier.</param>
/// <param name="Hops">Ordered list of swap hops forming the arbitrage path.</param>
/// <param name="InputAmountUsd">Notional input in USD.</param>
/// <param name="EstimatedOutputUsd">Estimated output after all swaps.</param>
/// <param name="GasEstimateUsd">Estimated gas cost in USD.</param>
/// <param name="NetProfitUsd">Estimated net profit after gas: <c>EstimatedOutputUsd − InputAmountUsd − GasEstimateUsd</c>.</param>
/// <param name="ExpiresAt">Earliest time at which price data used to compute this path becomes stale.</param>
public sealed record ArbPathFoundPayload(
    [property: JsonPropertyName("path_id")] Guid PathId,
    [property: JsonPropertyName("hops")] IReadOnlyList<ArbHop> Hops,
    [property: JsonPropertyName("input_amount_usd")] decimal InputAmountUsd,
    [property: JsonPropertyName("estimated_output_usd")] decimal EstimatedOutputUsd,
    [property: JsonPropertyName("gas_estimate_usd")] decimal GasEstimateUsd,
    [property: JsonPropertyName("net_profit_usd")] decimal NetProfitUsd,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);

/// <summary>A single swap hop within an arbitrage path.</summary>
/// <param name="FromToken">Input token symbol.</param>
/// <param name="ToToken">Output token symbol.</param>
/// <param name="Exchange">DEX exchange used for this hop (Camelot, DFYN, Balancer, Hyperliquid).</param>
/// <param name="Price">Execution price (ToToken per FromToken).</param>
public sealed record ArbHop(
    [property: JsonPropertyName("from_token")] string FromToken,
    [property: JsonPropertyName("to_token")] string ToToken,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("price")] decimal Price);
