namespace MLS.Arbitrager.Scanning;

/// <summary>
/// A live price snapshot for a token pair on a specific exchange.
/// </summary>
/// <param name="Exchange">Exchange identifier (e.g. <c>"camelot"</c>, <c>"hyperliquid"</c>).</param>
/// <param name="Symbol">Token pair (e.g. <c>"WETH/USDC"</c>).</param>
/// <param name="BidPrice">Best bid price.</param>
/// <param name="AskPrice">Best ask price.</param>
/// <param name="MidPrice">Mid price computed as <c>(Bid + Ask) / 2</c>.</param>
/// <param name="LiquidityUsd">Estimated available liquidity at mid price in USD.</param>
/// <param name="Timestamp">UTC time of the snapshot.</param>
public sealed record PriceSnapshot(
    string Exchange,
    string Symbol,
    decimal BidPrice,
    decimal AskPrice,
    decimal MidPrice,
    decimal LiquidityUsd,
    DateTimeOffset Timestamp);

/// <summary>
/// A single swap hop within an arbitrage path.
/// </summary>
/// <param name="FromToken">Input token symbol.</param>
/// <param name="ToToken">Output token symbol.</param>
/// <param name="Exchange">DEX exchange used for this hop.</param>
/// <param name="Price">Execution price (ToToken per FromToken).</param>
/// <param name="Fee">Swap fee fraction (e.g. 0.003 = 0.3%).</param>
/// <param name="GasEstimateUsd">Estimated gas cost in USD for this hop.</param>
public sealed record ArbHopDetail(
    string FromToken,
    string ToToken,
    string Exchange,
    decimal Price,
    decimal Fee,
    decimal GasEstimateUsd);

/// <summary>
/// A profitable arbitrage opportunity detected by the scanner.
/// </summary>
/// <param name="OpportunityId">Unique opportunity identifier.</param>
/// <param name="Hops">Ordered swap hops forming the circular path.</param>
/// <param name="InputAmountUsd">Simulated notional input in USD.</param>
/// <param name="EstimatedOutputUsd">Estimated output after all swaps in USD.</param>
/// <param name="GasEstimateUsd">Total estimated gas cost in USD.</param>
/// <param name="NetProfitUsd">Estimated net profit: <c>EstimatedOutputUsd − InputAmountUsd − GasEstimateUsd</c>.</param>
/// <param name="ProfitRatio">Net profit as a fraction of input amount.</param>
/// <param name="DetectedAt">UTC timestamp when the opportunity was detected.</param>
/// <param name="ExpiresAt">UTC timestamp when price data used is considered stale.</param>
public sealed record ArbitrageOpportunity(
    Guid OpportunityId,
    IReadOnlyList<ArbHopDetail> Hops,
    decimal InputAmountUsd,
    decimal EstimatedOutputUsd,
    decimal GasEstimateUsd,
    decimal NetProfitUsd,
    decimal ProfitRatio,
    DateTimeOffset DetectedAt,
    DateTimeOffset ExpiresAt);
