using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.DeFi.Hubs;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;
using MLS.DeFi.Persistence;

namespace MLS.DeFi.Controllers;

/// <summary>
/// HTTP API for DeFi strategy evaluation, order placement, and position management.
/// Base path: <c>/api/defi</c>.
/// </summary>
[ApiController]
[Route("api/defi")]
public sealed class DeFiController(
    IDeFiStrategyEngine _strategyEngine,
    IBrokerFallbackChain _fallbackChain,
    IHyperliquidClient _hyperliquid,
    IDbContextFactory<DeFiDbContext> _dbFactory,
    IHubContext<DeFiHub> _hub,
    ILogger<DeFiController> _logger) : ControllerBase
{
    private const string ModuleId = "defi";

    // ── Strategy ──────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/defi/strategy/evaluate — evaluate strategy without executing.
    /// </summary>
    [HttpPost("strategy/evaluate")]
    public async Task<IActionResult> EvaluateStrategy(
        [FromBody] DeFiStrategyRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            return BadRequest(new { error = "symbol is required" });

        var result = await _strategyEngine.EvaluateAsync(request, ct).ConfigureAwait(false);

        _logger.LogInformation("Strategy evaluated: symbol={Symbol} strategy={Strategy} venue={Venue}",
            DeFiUtils.SafeLog(request.Symbol), result.StrategyType, result.Venue);

        return Ok(result);
    }

    /// <summary>
    /// POST /api/defi/strategy/execute — select optimal strategy and execute it.
    /// </summary>
    [HttpPost("strategy/execute")]
    public async Task<IActionResult> ExecuteStrategy(
        [FromBody] DeFiStrategyRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            return BadRequest(new { error = "symbol is required" });

        DeFiStrategyResult result;
        try
        {
            result = await _strategyEngine.ExecuteAsync(request, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Strategy execution failed for symbol={Symbol}",
                DeFiUtils.SafeLog(request.Symbol));
            return StatusCode(503, new { error = ex.Message });
        }

        var envelope = EnvelopePayload.Create(MessageTypes.DeFiStrategyExecuted, ModuleId, result);
        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveEnvelope", envelope, ct)
                  .ConfigureAwait(false);

        return Accepted(result);
    }

    /// <summary>
    /// GET /api/defi/venues — returns all currently healthy execution venues.
    /// </summary>
    [HttpGet("venues")]
    public async Task<IActionResult> GetAvailableVenues(CancellationToken ct)
    {
        var venues = await _strategyEngine.GetAvailableVenuesAsync(ct).ConfigureAwait(false);
        return Ok(venues);
    }

    // ── Orders ────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/defi/orders — place a new order directly via the fallback chain.
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> PlaceOrder(
        [FromBody] DeFiOrderRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientOrderId))
            return BadRequest(new { error = "clientOrderId is required" });

        DeFiOrderResult result;
        try
        {
            result = await _fallbackChain.ExecuteWithFallbackAsync(request, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "All venues failed for order {ClientOrderId}",
                DeFiUtils.SafeLog(request.ClientOrderId));
            return StatusCode(503, new { error = ex.Message });
        }

        // Persist the transaction
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo = new TransactionRepository(db);
        await repo.InsertAsync(MapToEntity(request, result), ct).ConfigureAwait(false);

        var messageType = result.State == DeFiOrderState.Rejected
            ? MessageTypes.OrderRejection
            : MessageTypes.OrderConfirmation;

        var envelope = EnvelopePayload.Create(messageType, ModuleId, result);
        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveEnvelope", envelope, ct)
                  .ConfigureAwait(false);

        _logger.LogInformation("ORDER_CREATE: {ClientOrderId} state={State} venue={Venue}",
            DeFiUtils.SafeLog(result.ClientOrderId), result.State, result.Venue);

        return result.State == DeFiOrderState.Rejected
            ? UnprocessableEntity(result)
            : Accepted(result);
    }

    /// <summary>
    /// DELETE /api/defi/orders/{clientOrderId} — cancel an open order.
    /// </summary>
    [HttpDelete("orders/{clientOrderId}")]
    public async Task<IActionResult> CancelOrder(string clientOrderId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientOrderId))
            return BadRequest(new { error = "clientOrderId is required" });

        var cancelResult = await _hyperliquid.CancelOrderAsync(clientOrderId, ct)
                                              .ConfigureAwait(false);

        if (cancelResult.State == DeFiOrderState.Rejected)
        {
            _logger.LogWarning("Venue rejected cancel for {ClientOrderId}",
                DeFiUtils.SafeLog(clientOrderId));
            return Conflict(new { error = "Venue could not cancel the order" });
        }

        var envelope = EnvelopePayload.Create(MessageTypes.OrderCancel, ModuleId,
            new { clientOrderId, state = DeFiOrderState.Cancelled });

        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveEnvelope", envelope, ct)
                  .ConfigureAwait(false);

        _logger.LogInformation("ORDER_CANCEL: {ClientOrderId}", DeFiUtils.SafeLog(clientOrderId));
        return Ok(new { clientOrderId, state = DeFiOrderState.Cancelled });
    }

    // ── Positions ─────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/defi/positions/{symbol} — returns the current open position.
    /// Returns 404 when no position exists.
    /// </summary>
    [HttpGet("positions/{symbol}")]
    public async Task<IActionResult> GetPosition(string symbol, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "symbol is required" });

        var position = await _hyperliquid.GetPositionAsync(symbol, ct).ConfigureAwait(false);

        if (position is null)
            return NotFound(new { symbol, message = "No open position found" });

        _logger.LogInformation("Position query: {Symbol} qty={Qty} pnl={Pnl}",
            DeFiUtils.SafeLog(symbol), position.Quantity, position.UnrealisedPnl);

        return Ok(position);
    }

    /// <summary>
    /// POST /api/defi/positions/{symbol}/refresh — refresh position and broadcast envelope.
    /// </summary>
    [HttpPost("positions/{symbol}/refresh")]
    public async Task<IActionResult> RefreshPosition(string symbol, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "symbol is required" });

        var position = await _hyperliquid.GetPositionAsync(symbol, ct).ConfigureAwait(false);

        if (position is null)
            return NotFound(new { symbol, message = "No open position found" });

        var envelope = EnvelopePayload.Create(MessageTypes.PositionUpdate, ModuleId, position);
        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveEnvelope", envelope, ct)
                  .ConfigureAwait(false);

        _logger.LogInformation("POSITION_UPDATE broadcast: {Symbol} qty={Qty}",
            DeFiUtils.SafeLog(symbol), position.Quantity);

        return Ok(position);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static TransactionEntity MapToEntity(DeFiOrderRequest req, DeFiOrderResult result)
        => new()
        {
            ClientOrderId      = req.ClientOrderId,
            VenueOrTxId        = result.VenueOrderId,
            Symbol             = req.Symbol,
            Side               = req.Side.ToString(),
            OrderType          = req.Type.ToString(),
            Quantity           = req.Quantity,
            LimitPrice         = req.LimitPrice,
            State              = result.State.ToString(),
            FilledQuantity     = result.FilledQuantity,
            AveragePrice       = result.AveragePrice,
            Venue              = result.Venue,
            RequestingModuleId = req.RequestingModuleId,
            CreatedAt          = result.CreatedAt,
            UpdatedAt          = result.UpdatedAt,
        };
}
