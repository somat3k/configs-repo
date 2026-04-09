using System.Runtime.CompilerServices;
using MLS.Broker.Models;

namespace MLS.Broker.Interfaces;

/// <summary>
/// Tracks in-flight and completed orders with state persistence to PostgreSQL
/// and a Redis hot cache for low-latency lookups.
/// </summary>
public interface IOrderTracker
{
    /// <summary>
    /// Persists a newly created <paramref name="order"/> to the backing store.
    /// No-ops if an order with the same <see cref="OrderResult.ClientOrderId"/> already exists
    /// (idempotency guarantee).
    /// </summary>
    Task TrackAsync(OrderResult order, CancellationToken ct);

    /// <summary>
    /// Applies a state transition to an existing order identified by <paramref name="clientOrderId"/>.
    /// Updates both the PostgreSQL record and the Redis cache.
    /// </summary>
    Task UpdateAsync(
        string clientOrderId,
        OrderState newState,
        decimal filledQuantity,
        decimal? averagePrice,
        CancellationToken ct);

    /// <summary>
    /// Returns the latest <see cref="OrderResult"/> for the given <paramref name="clientOrderId"/>,
    /// or <see langword="null"/> when the order is unknown.
    /// </summary>
    Task<OrderResult?> GetAsync(string clientOrderId, CancellationToken ct);

    /// <summary>
    /// Streams all orders whose <see cref="OrderState"/> is <see cref="OrderState.Open"/>
    /// or <see cref="OrderState.PartiallyFilled"/>.
    /// </summary>
    IAsyncEnumerable<OrderResult> GetOpenOrdersAsync(CancellationToken ct);
}
