using System.Runtime.CompilerServices;
using MLS.DeFi.Models;

namespace MLS.DeFi.Interfaces;

/// <summary>
/// HYPERLIQUID REST + WebSocket client abstraction for the DeFi module.
/// Implementations connect to the HYPERLIQUID API endpoints loaded from configuration.
/// </summary>
public interface IHyperliquidClient
{
    /// <summary>Places a new order on HYPERLIQUID via the REST API.</summary>
    Task<DeFiOrderResult> PlaceOrderAsync(DeFiOrderRequest request, CancellationToken ct);

    /// <summary>Cancels an open order by its <paramref name="clientOrderId"/>.</summary>
    Task<DeFiOrderResult> CancelOrderAsync(string clientOrderId, CancellationToken ct);

    /// <summary>Returns all currently open orders for the given <paramref name="symbol"/>.</summary>
    Task<IReadOnlyList<DeFiOrderResult>> GetOpenOrdersAsync(string symbol, CancellationToken ct);

    /// <summary>
    /// Returns the current open position for the given <paramref name="symbol"/>,
    /// or <see langword="null"/> when no position exists.
    /// </summary>
    Task<DeFiPositionSnapshot?> GetPositionAsync(string symbol, CancellationToken ct);

    /// <summary>
    /// Subscribes to the fill notification stream for all open orders.
    /// Yields one <see cref="DeFiFillNotification"/> per partial or full fill event.
    /// </summary>
    IAsyncEnumerable<DeFiFillNotification> SubscribeFillsAsync(CancellationToken ct);

    /// <summary>
    /// Subscribes to the order book depth stream for the given <paramref name="symbol"/>.
    /// Yields one <see cref="DeFiOrderBookUpdate"/> per received depth snapshot.
    /// </summary>
    IAsyncEnumerable<DeFiOrderBookUpdate> SubscribeOrderBookAsync(string symbol, CancellationToken ct);
}
