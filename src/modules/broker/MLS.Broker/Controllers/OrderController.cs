using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MLS.Broker.Hubs;
using MLS.Broker.Interfaces;
using MLS.Broker.Models;
using MLS.Core.Constants;
using MLS.Core.Contracts;

namespace MLS.Broker.Controllers;

/// <summary>
/// HTTP API for order management.
/// Base path: <c>/api/orders</c>.
/// </summary>
[ApiController]
[Route("api/orders")]
public sealed class OrderController(
    IBrokerFallbackChain _fallbackChain,
    IOrderTracker _orderTracker,
    IHyperliquidClient _hyperliquid,
    IHubContext<BrokerHub> _hub,
    ILogger<OrderController> _logger) : ControllerBase
{
    private const string ModuleId = "broker";

    /// <summary>
    /// POST /api/orders — place a new order on the primary venue (HYPERLIQUID) with fallback.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientOrderId))
            return BadRequest(new { error = "clientOrderId is required" });

        // Check idempotency — reject duplicates
        var existing = await _orderTracker.GetAsync(request.ClientOrderId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            _logger.LogInformation("Idempotent ORDER_CREATE — returning existing order {ClientOrderId}",
                BrokerUtils.SafeLog(request.ClientOrderId));
            return Ok(existing);
        }

        OrderResult result;
        try
        {
            result = await _fallbackChain.ExecuteWithFallbackAsync(request, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "All venues failed for order {ClientOrderId}", BrokerUtils.SafeLog(request.ClientOrderId));
            return StatusCode(503, new { error = ex.Message });
        }

        // Track the order
        await _orderTracker.TrackAsync(request, result, ct).ConfigureAwait(false);

        // Broadcast ORDER_CONFIRMATION for accepted orders; ORDER_REJECTION for rejected.
        var messageType = result.State == OrderState.Rejected
            ? MessageTypes.OrderRejection
            : MessageTypes.OrderConfirmation;
        var envelope = EnvelopePayload.Create(messageType, ModuleId, result);
        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveEnvelope", envelope, ct)
                  .ConfigureAwait(false);

        _logger.LogInformation("ORDER_CREATE: {ClientOrderId} state={State} venue={Venue}",
            BrokerUtils.SafeLog(result.ClientOrderId), result.State, result.Venue);

        return result.State == OrderState.Rejected
            ? UnprocessableEntity(result)
            : Accepted(result);
    }

    /// <summary>
    /// DELETE /api/orders/{clientOrderId} — cancel an open order.
    /// </summary>
    [HttpDelete("{clientOrderId}")]
    public async Task<IActionResult> CancelOrder(string clientOrderId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientOrderId))
            return BadRequest(new { error = "clientOrderId is required" });

        var order = await _orderTracker.GetAsync(clientOrderId, ct).ConfigureAwait(false);
        if (order is null)
            return NotFound(new { error = $"Order '{clientOrderId}' not found" });

        if (order.State is OrderState.Filled or OrderState.Cancelled or OrderState.Rejected)
            return Conflict(new { error = $"Cannot cancel order in state '{order.State}'" });

        // Call HYPERLIQUID cancel API first — only update local state after venue confirms.
        var cancelResult = await _hyperliquid.CancelOrderAsync(clientOrderId, ct).ConfigureAwait(false);
        if (cancelResult.State == OrderState.Rejected)
        {
            _logger.LogWarning("Venue rejected cancel for {ClientOrderId}", BrokerUtils.SafeLog(clientOrderId));
            return Conflict(new { error = "Venue could not cancel the order — it may have already been filled" });
        }

        await _orderTracker.UpdateAsync(clientOrderId, OrderState.Cancelled, order.FilledQuantity, order.AveragePrice, ct)
                           .ConfigureAwait(false);

        var envelope = EnvelopePayload.Create(MessageTypes.OrderCancel, ModuleId,
            order with { State = OrderState.Cancelled, UpdatedAt = DateTimeOffset.UtcNow });

        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveEnvelope", envelope, ct)
                  .ConfigureAwait(false);

        _logger.LogInformation("ORDER_CANCEL accepted: {ClientOrderId}", BrokerUtils.SafeLog(clientOrderId));

        return Ok(new { clientOrderId, state = OrderState.Cancelled });
    }

    /// <summary>
    /// GET /api/orders/{clientOrderId} — retrieve order by client ID.
    /// </summary>
    [HttpGet("{clientOrderId}")]
    public async Task<IActionResult> GetOrder(string clientOrderId, CancellationToken ct)
    {
        var order = await _orderTracker.GetAsync(clientOrderId, ct).ConfigureAwait(false);
        return order is null
            ? NotFound(new { error = $"Order '{clientOrderId}' not found" })
            : Ok(order);
    }

    /// <summary>
    /// GET /api/orders/open — returns all open and partially-filled orders.
    /// </summary>
    [HttpGet("open")]
    public async IAsyncEnumerable<OrderResult> GetOpenOrders(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var order in _orderTracker.GetOpenOrdersAsync(ct).ConfigureAwait(false))
            yield return order;
    }
}
