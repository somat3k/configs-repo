using Microsoft.AspNetCore.Mvc;

namespace MLS.Transactions.Controllers;

/// <summary>REST API controller for the Transactions module.</summary>
[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(
    ILogger<TransactionsController> _logger) : ControllerBase
{
    /// <summary>Health check endpoint.</summary>
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "healthy", module = "transactions", timestamp = DateTimeOffset.UtcNow });
}

/// <summary>Root health check controller.</summary>
[ApiController]
[Route("")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns 200 OK. Used for container health checks.</summary>
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "healthy", module = "transactions", timestamp = DateTimeOffset.UtcNow });
}
