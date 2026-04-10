using MLS.DeFi.Models;

namespace MLS.DeFi.Interfaces;

/// <summary>
/// Selects and executes the optimal DeFi strategy for a given trade request.
/// Strategy selection considers available venues (HYPERLIQUID, Camelot, DFYN,
/// Balancer, Morpho, nHOP), liquidity, fees, and configured slippage limits.
/// </summary>
/// <remarks>
/// Rules:
/// <list type="bullet">
///   <item>NEVER select Uniswap as a venue.</item>
///   <item>HYPERLIQUID is preferred for perpetual futures.</item>
///   <item>All address lookups go through <c>IOnChainTransactionService</c>.</item>
/// </list>
/// </remarks>
public interface IDeFiStrategyEngine
{
    /// <summary>
    /// Evaluates available venues and returns the best execution strategy
    /// without submitting an order.
    /// </summary>
    Task<DeFiStrategyResult> EvaluateAsync(DeFiStrategyRequest request, CancellationToken ct);

    /// <summary>
    /// Selects the optimal strategy, submits the order or on-chain transaction,
    /// and returns the execution result.
    /// </summary>
    Task<DeFiStrategyResult> ExecuteAsync(DeFiStrategyRequest request, CancellationToken ct);

    /// <summary>
    /// Returns the list of currently available (healthy) execution venues.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableVenuesAsync(CancellationToken ct);
}
