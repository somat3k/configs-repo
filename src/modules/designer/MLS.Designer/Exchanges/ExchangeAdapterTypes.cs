namespace MLS.Designer.Exchanges;

/// <summary>
/// A request to execute a swap on a DEX or lending protocol.
/// </summary>
/// <param name="TokenIn">Input token symbol (e.g. <c>"WETH"</c>).</param>
/// <param name="TokenOut">Output token symbol (e.g. <c>"USDC"</c>).</param>
/// <param name="AmountIn">Input token amount (in token units).</param>
/// <param name="ExpectedAmountOut">Expected output before slippage; 0 means no pre-check.</param>
/// <param name="SlippageTolerance">Maximum tolerated slippage fraction (e.g. 0.005 = 0.5%).</param>
/// <param name="Recipient">Destination address for the output tokens.</param>
/// <param name="DeadlineUtc">Transaction deadline; rejected on-chain after this timestamp.</param>
public sealed record SwapRequest(
    string TokenIn,
    string TokenOut,
    decimal AmountIn,
    decimal ExpectedAmountOut,
    decimal SlippageTolerance,
    string Recipient,
    DateTimeOffset DeadlineUtc);

/// <summary>
/// Result of an executed swap.
/// </summary>
/// <param name="TransactionHash">On-chain transaction hash.</param>
/// <param name="AmountIn">Actual amount of <c>TokenIn</c> consumed.</param>
/// <param name="AmountOut">Actual amount of <c>TokenOut</c> received.</param>
/// <param name="GasUsed">Gas units used by the transaction.</param>
/// <param name="GasPriceGwei">Effective gas price in Gwei.</param>
/// <param name="ExecutedAt">UTC timestamp of on-chain confirmation.</param>
public sealed record SwapResult(
    string TransactionHash,
    decimal AmountIn,
    decimal AmountOut,
    ulong GasUsed,
    decimal GasPriceGwei,
    DateTimeOffset ExecutedAt);

/// <summary>
/// A real-time price tick from a subscribed exchange feed.
/// </summary>
/// <param name="Exchange">Exchange identifier (e.g. <c>"camelot"</c>).</param>
/// <param name="Symbol">Token pair symbol (e.g. <c>"WETH/USDC"</c>).</param>
/// <param name="BidPrice">Best bid price.</param>
/// <param name="AskPrice">Best ask price.</param>
/// <param name="MidPrice">Mid price: <c>(Bid + Ask) / 2</c>.</param>
/// <param name="Liquidity">Available liquidity at mid price in USD.</param>
/// <param name="Timestamp">UTC timestamp of the price tick.</param>
public sealed record PriceUpdate(
    string Exchange,
    string Symbol,
    decimal BidPrice,
    decimal AskPrice,
    decimal MidPrice,
    decimal Liquidity,
    DateTimeOffset Timestamp);

/// <summary>
/// A snapshot of the order book at a given depth.
/// </summary>
/// <param name="Exchange">Exchange identifier.</param>
/// <param name="Symbol">Token pair symbol.</param>
/// <param name="Bids">Bid levels: <c>(price, size)</c> sorted best first.</param>
/// <param name="Asks">Ask levels: <c>(price, size)</c> sorted best first.</param>
/// <param name="Timestamp">UTC timestamp of the snapshot.</param>
public sealed record OrderBookSnapshot(
    string Exchange,
    string Symbol,
    IReadOnlyList<(decimal Price, decimal Size)> Bids,
    IReadOnlyList<(decimal Price, decimal Size)> Asks,
    DateTimeOffset Timestamp)
{
    /// <summary>Estimate execution price for the given <paramref name="amountIn"/> using VWAP.</summary>
    public decimal EstimateExecutionPrice(decimal amountIn, bool isBuy)
    {
        var levels = isBuy ? Asks : Bids;
        decimal remaining = amountIn;
        decimal totalCost = 0m;

        foreach (var (price, size) in levels)
        {
            var fill = Math.Min(remaining, size);
            totalCost += fill * price;
            remaining -= fill;
            if (remaining <= 0) break;
        }

        return amountIn > 0 ? totalCost / amountIn : 0m;
    }
}

/// <summary>
/// Thrown when a live online price source is unreachable or returns no data.
/// Callers must <b>not</b> substitute synthetic or cached fallback prices;
/// the strategy engine will handle the unavailability through its backoff/retry policy.
/// </summary>
public sealed class ExchangeUnavailableException(string exchangeId, string baseToken, string quoteToken)
    : Exception(
        $"Exchange '{exchangeId}' could not provide a live price for {baseToken}/{quoteToken}. " +
        $"No fallback data is permitted — live online source required.")
{
    /// <summary>Exchange identifier that failed.</summary>
    public string ExchangeId { get; } = exchangeId;

    /// <summary>Base token of the requested pair.</summary>
    public string BaseToken { get; } = baseToken;

    /// <summary>Quote token of the requested pair.</summary>
    public string QuoteToken { get; } = quoteToken;
}

/// <summary>
/// Thrown when the market has moved beyond the configured slippage tolerance.
/// </summary>
public sealed class SlippageExceededException(
    SwapRequest request,
    decimal actualExpectedOut,
    decimal requestedExpectedOut)
    : Exception(
        $"Slippage exceeded for {request.TokenIn}→{request.TokenOut}: " +
        $"expected {requestedExpectedOut:F6}, market offers {actualExpectedOut:F6} " +
        $"(tolerance {request.SlippageTolerance:P2})")
{
    /// <summary>The original swap request.</summary>
    public SwapRequest Request { get; } = request;

    /// <summary>Actual expected output based on current market price.</summary>
    public decimal ActualExpectedOut { get; } = actualExpectedOut;

    /// <summary>Requested expected output in the original request.</summary>
    public decimal RequestedExpectedOut { get; } = requestedExpectedOut;
}
