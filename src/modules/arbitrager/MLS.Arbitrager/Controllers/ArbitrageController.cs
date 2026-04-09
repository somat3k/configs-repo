using Microsoft.AspNetCore.Mvc;
using MLS.Arbitrager.Addresses;
using MLS.Arbitrager.Scanning;

namespace MLS.Arbitrager.Controllers;

/// <summary>
/// HTTP API for the Arbitrager module.
/// Base path: <c>/api/arbitrage</c>.
/// </summary>
[ApiController]
[Route("api/arbitrage")]
public sealed class ArbitrageController(
    IOpportunityScanner _scanner,
    IArbitragerAddressBook _addressBook,
    ILogger<ArbitrageController> _logger) : ControllerBase
{
    /// <summary>
    /// GET /api/arbitrage/prices — returns all current price snapshots.
    /// </summary>
    [HttpGet("prices")]
    public ActionResult<IReadOnlyDictionary<string, PriceSnapshot>> GetPrices()
        => Ok(_scanner.GetCurrentPrices());

    /// <summary>
    /// POST /api/arbitrage/prices — inject a price snapshot into the scanner (for testing).
    /// </summary>
    [HttpPost("prices")]
    public IActionResult InjectPrice([FromBody] PriceSnapshot snapshot)
    {
        _scanner.PublishPrice(snapshot);
        _logger.LogInformation("ArbitrageController: injected price for {Exchange}/{Symbol}",
            snapshot.Exchange, snapshot.Symbol);
        return Accepted(new { message = "Price injected", snapshot });
    }

    /// <summary>
    /// GET /api/arbitrage/addresses — returns all loaded blockchain addresses.
    /// </summary>
    [HttpGet("addresses")]
    public ActionResult<IReadOnlyDictionary<string, string>> GetAddresses()
    {
        var result = _addressBook.All
            .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/arbitrage/addresses/refresh — reloads all addresses from PostgreSQL.
    /// </summary>
    [HttpPost("addresses/refresh")]
    public async Task<IActionResult> RefreshAddresses(CancellationToken ct)
    {
        await _addressBook.RefreshAsync(ct).ConfigureAwait(false);
        return Ok(new { message = "Address book refreshed", count = _addressBook.All.Count });
    }
}
