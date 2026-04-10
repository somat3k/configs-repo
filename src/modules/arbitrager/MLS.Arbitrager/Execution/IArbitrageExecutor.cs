using MLS.Arbitrager.Scanning;

namespace MLS.Arbitrager.Execution;

/// <summary>
/// Coordinates the end-to-end arbitrage execution pipeline:
/// score → build array → dispatch to Transactions module via Block Controller.
/// </summary>
public interface IArbitrageExecutor
{
    /// <summary>
    /// Processes the given opportunity: scores it, builds a transaction array,
    /// and dispatches it to the Transactions module if confidence exceeds threshold.
    /// </summary>
    /// <param name="opportunity">The detected opportunity to process.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExecuteAsync(ArbitrageOpportunity opportunity, CancellationToken ct);
}
