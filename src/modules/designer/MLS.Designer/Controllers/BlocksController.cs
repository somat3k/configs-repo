using Microsoft.AspNetCore.Mvc;
using MLS.Designer.Services;

namespace MLS.Designer.Controllers;

/// <summary>
/// REST API for block type discovery.
/// </summary>
[ApiController]
[Route("api/blocks")]
public sealed class BlocksController(
    IBlockRegistry _registry,
    ILogger<BlocksController> _logger) : ControllerBase
{
    /// <summary>Return all registered block types with metadata.</summary>
    /// <response code="200">List of block metadata records.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BlockMetadata>>(StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        var blocks = _registry.GetAll();
        _logger.LogDebug("GET /api/blocks — returning {Count} block types", blocks.Count);
        return Ok(blocks);
    }

    /// <summary>Get metadata for a specific block type.</summary>
    /// <param name="key">Registry key, e.g. <c>RSIBlock</c>.</param>
    /// <response code="200">Block metadata.</response>
    /// <response code="404">Block type not found.</response>
    [HttpGet("{key}")]
    [ProducesResponseType<BlockMetadata>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByKey(string key)
    {
        // Sanitize user-supplied key before logging to prevent log-forging (CWE-117)
        var safeKey = key.Length > 128
            ? key[..128].Replace('\r', '_').Replace('\n', '_')
            : key.Replace('\r', '_').Replace('\n', '_');

        var metadata = _registry.GetByKey(key);
        if (metadata is null)
        {
            _logger.LogDebug("GET /api/blocks/{Key} — not found", safeKey);
            return NotFound(new { error = $"Block type '{safeKey}' is not registered." });
        }
        return Ok(metadata);
    }
}

/// <summary>Health check endpoint for container readiness probes.</summary>
[ApiController]
[Route("")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns 200 OK with a status object.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new { status = "healthy", module = "designer", timestamp = DateTimeOffset.UtcNow });
}
