using MLS.Broker.Models;

namespace MLS.Broker.Interfaces;

/// <summary>
/// Executes order placement with automatic failover across configured broker venues.
/// The primary venue is always HYPERLIQUID; subsequent venues are tried in order
/// when a higher-priority venue is unavailable or rejects the order.
/// </summary>
public interface IBrokerFallbackChain
{
    /// <summary>
    /// Attempts to place <paramref name="request"/> on the primary venue.
    /// On failure, cascades through the configured fallback venues.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when all venues in the chain have been exhausted without success.
    /// </exception>
    Task<OrderResult> ExecuteWithFallbackAsync(PlaceOrderRequest request, CancellationToken ct);

    /// <summary>
    /// Returns the IDs of currently healthy (reachable) broker venues.
    /// </summary>
    Task<IReadOnlyList<string>> GetActiveBrokersAsync(CancellationToken ct);
}
