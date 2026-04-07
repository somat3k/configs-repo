using Microsoft.AspNetCore.Mvc;
using MLS.Core.Contracts.Designer;
using MLS.DataLayer.Hydra;

namespace MLS.DataLayer.Controllers;

/// <summary>
/// HTTP API for managing Hydra feed collection jobs and querying feed status.
/// Base path: <c>/api/feeds</c>.
/// </summary>
[ApiController]
[Route("api/feeds")]
public sealed class FeedController(
    FeedScheduler _scheduler,
    ILogger<FeedController> _logger) : ControllerBase
{
    /// <summary>
    /// GET /api/feeds — returns all currently active feed subscriptions.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<FeedKey>> GetActiveFeeds()
        => Ok(_scheduler.ActiveFeeds());

    /// <summary>
    /// POST /api/feeds — starts a new feed collection job from a
    /// <c>DATA_COLLECTION_START</c> payload.
    /// </summary>
    [HttpPost]
    public IActionResult StartFeed([FromBody] DataCollectionStartPayload request)
    {
        var key     = new FeedKey(request.Exchange, request.Symbol, request.Timeframe);
        var started = _scheduler.StartFeed(key);

        if (!started)
        {
            return Conflict(new { message = "Feed already active", key });
        }

        _logger.LogInformation("FeedController: started [{Exchange}/{Symbol}/{Timeframe}]",
            HydraUtils.SanitiseFeedId(request.Exchange),
            HydraUtils.SanitiseFeedId(request.Symbol),
            HydraUtils.SanitiseFeedId(request.Timeframe));

        return Accepted(new { message = "Feed started", key });
    }

    /// <summary>
    /// DELETE /api/feeds/{exchange}/{symbol}/{timeframe} — stops a feed job.
    /// </summary>
    [HttpDelete("{exchange}/{symbol}/{timeframe}")]
    public async Task<IActionResult> StopFeed(
        string exchange, string symbol, string timeframe,
        CancellationToken ct)
    {
        var key = new FeedKey(exchange, symbol, timeframe);

        if (_scheduler.GetStatus(key) == "NotFound")
            return NotFound(new { message = "Feed not active", key });

        await _scheduler.StopFeedAsync(key).ConfigureAwait(false);

        _logger.LogInformation("FeedController: stopped [{Exchange}/{Symbol}/{Timeframe}]",
            HydraUtils.SanitiseFeedId(exchange),
            HydraUtils.SanitiseFeedId(symbol),
            HydraUtils.SanitiseFeedId(timeframe));

        return Ok(new { message = "Feed stopped", key });
    }

    /// <summary>
    /// GET /api/feeds/{exchange}/{symbol}/{timeframe}/status — returns job status.
    /// </summary>
    [HttpGet("{exchange}/{symbol}/{timeframe}/status")]
    public IActionResult GetStatus(string exchange, string symbol, string timeframe)
    {
        var key    = new FeedKey(exchange, symbol, timeframe);
        var status = _scheduler.GetStatus(key);
        return Ok(new { key, status });
    }
}
