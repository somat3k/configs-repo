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
    Guid PathId,
    IReadOnlyList<ArbHop> Hops,
    decimal InputAmountUsd,
    decimal EstimatedOutputUsd,
    decimal GasEstimateUsd,
    decimal NetProfitUsd,
    DateTimeOffset ExpiresAt);

/// <summary>A single swap hop within an arbitrage path.</summary>
/// <param name="FromToken">Input token symbol.</param>
/// <param name="ToToken">Output token symbol.</param>
/// <param name="Exchange">DEX exchange used for this hop (Camelot, DFYN, Balancer, Hyperliquid).</param>
/// <param name="Price">Execution price (ToToken per FromToken).</param>
public sealed record ArbHop(string FromToken, string ToToken, string Exchange, decimal Price);
