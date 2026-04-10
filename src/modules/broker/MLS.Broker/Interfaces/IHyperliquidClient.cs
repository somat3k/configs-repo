using System.Runtime.CompilerServices;
using MLS.Broker.Models;

namespace MLS.Broker.Interfaces;

/// <summary>
/// HYPERLIQUID REST + WebSocket client abstraction.
/// Implementations connect to the HYPERLIQUID API endpoints loaded from configuration.
/// </summary>
public interface IHyperliquidClient
{
    /// <summary>
    /// Places a new order on HYPERLIQUID via the REST API.
    /// The <see cref="PlaceOrderRequest.ClientOrderId"/> is used as the idempotency key.
    /// </summary>
    Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct);

    /// <summary>
    /// Cancels an open order by its <paramref name="clientOrderId"/>.
    /// </summary>
    Task<OrderResult> CancelOrderAsync(string clientOrderId, CancellationToken ct);

    /// <summary>
    /// Returns all currently open orders for the given <paramref name="symbol"/>.
    /// </summary>
    Task<IReadOnlyList<OrderResult>> GetOpenOrdersAsync(string symbol, CancellationToken ct);

    /// <summary>
    /// Returns the current open position for the given <paramref name="symbol"/>,
    /// or <see langword="null"/> when no position exists.
    /// </summary>
    Task<PositionSnapshot?> GetPositionAsync(string symbol, CancellationToken ct);

    /// <summary>
    /// Subscribes to the fill notification stream for all open orders.
    /// Yields one <see cref="FillNotification"/> per partial or full fill event.
    /// </summary>
    IAsyncEnumerable<FillNotification> SubscribeFillsAsync(CancellationToken ct);

    /// <summary>
    /// Subscribes to the order book depth stream for the given <paramref name="symbol"/>.
    /// Yields one <see cref="OrderBookUpdate"/> per received depth snapshot.
    /// </summary>
    IAsyncEnumerable<OrderBookUpdate> SubscribeOrderBookAsync(
        string symbol,
        CancellationToken ct);
}
