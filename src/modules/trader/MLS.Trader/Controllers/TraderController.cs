using Microsoft.AspNetCore.Mvc;
using MLS.Trader.Interfaces;
using MLS.Trader.Services;

namespace MLS.Trader.Controllers;

/// <summary>
/// HTTP API for the Trader module.
/// Base path: <c>/api/trader</c>.
/// </summary>
[ApiController]
[Route("api/trader")]
public sealed class TraderController(
    IOrderManager _orderManager,
    MarketDataWorker _worker,
    ILogger<TraderController> _logger) : ControllerBase
{
    /// <summary>
    /// GET /api/trader/orders — streams all open or pending orders.
    /// </summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetOpenOrders(CancellationToken ct)
    {
        var orders = new List<object>();

        await foreach (var order in _orderManager.GetOpenOrdersAsync(ct).ConfigureAwait(false))
        {
            orders.Add(new
            {
                client_order_id  = order.ClientOrderId,
                symbol           = order.Symbol,
                direction        = order.Direction.ToString(),
                quantity         = order.Quantity,
                entry_price      = order.EntryPrice,
                stop_loss        = order.StopLossPrice,
                take_profit      = order.TakeProfitPrice,
                state            = order.State.ToString(),
                paper_trading    = order.PaperTrading,
                created_at       = order.CreatedAt,
                updated_at       = order.UpdatedAt,
            });
        }

        return Ok(orders);
    }

    /// <summary>
    /// GET /api/trader/positions — returns all cached open positions.
    /// </summary>
    [HttpGet("positions")]
    public IActionResult GetPositions()
    {
        var positions = _worker.Positions.Values.Select(p => new
        {
            symbol             = p.Symbol,
            direction          = p.Direction.ToString(),
            quantity           = p.Quantity,
            average_entry      = p.AverageEntryPrice,
            unrealised_pnl     = p.UnrealisedPnl,
            venue              = p.Venue,
            updated_at         = p.UpdatedAt,
        });

        return Ok(positions);
    }

    /// <summary>
    /// DELETE /api/trader/orders/{clientOrderId} — cancels an open order.
    /// </summary>
    [HttpDelete("orders/{clientOrderId}")]
    public async Task<IActionResult> CancelOrder(string clientOrderId, CancellationToken ct)
    {
        var order = await _orderManager.GetOrderAsync(clientOrderId, ct).ConfigureAwait(false);
        if (order is null)
            return NotFound(new { error = $"Order {clientOrderId} not found." });

        await _orderManager.CancelOrderAsync(clientOrderId, ct).ConfigureAwait(false);

        _logger.LogInformation("TraderController: order {ClientOrderId} cancelled via HTTP API", clientOrderId);
        return Ok(new { message = "Order cancelled", client_order_id = clientOrderId });
    }
}
