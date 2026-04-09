using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MLS.Broker.Hubs;
using MLS.Broker.Interfaces;
using MLS.Broker.Models;
using MLS.Core.Constants;
using MLS.Core.Contracts;

namespace MLS.Broker.Controllers;

/// <summary>
/// HTTP API for position management.
/// Base path: <c>/api/positions</c>.
/// </summary>
[ApiController]
[Route("api/positions")]
public sealed class PositionController(
    IHyperliquidClient _hyperliquid,
    IHubContext<BrokerHub> _hub,
    ILogger<PositionController> _logger) : ControllerBase
{
    private const string ModuleId = "broker";

    /// <summary>
    /// GET /api/positions/{symbol} — returns the current open position for the given symbol.
    /// Returns 404 when no position exists.
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetPosition(string symbol, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "symbol is required" });

        var position = await _hyperliquid.GetPositionAsync(symbol, ct).ConfigureAwait(false);

        if (position is null)
            return NotFound(new { symbol, message = "No open position found" });

        _logger.LogInformation("Position query: {Symbol} qty={Qty} pnl={Pnl}",
            symbol, position.Quantity, position.UnrealisedPnl);

        return Ok(position);
    }

    /// <summary>
    /// POST /api/positions/{symbol}/refresh — fetches the latest position from the venue and
    /// broadcasts a <c>POSITION_UPDATE</c> envelope.
    /// </summary>
    [HttpPost("{symbol}/refresh")]
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

        _logger.LogInformation("POSITION_UPDATE broadcast: {Symbol} qty={Qty}", symbol, position.Quantity);

        return Ok(position);
    }
}
