using Microsoft.AspNetCore.Mvc;
using MLS.BlockController.Models;
using MLS.BlockController.Services;

namespace MLS.BlockController.Controllers;

/// <summary>
/// HTTP REST API for module registration and discovery.
/// </summary>
[ApiController]
[Route("api/modules")]
public sealed class ModulesController(
    IModuleRegistry _registry,
    ILogger<ModulesController> _logger) : ControllerBase
{
    /// <summary>Register a module and return its assigned registration record.</summary>
    /// <response code="200">Registration successful.</response>
    /// <response code="400">Invalid request body.</response>
    [HttpPost("register")]
    [ProducesResponseType<ModuleRegistration>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAsync(
        [FromBody] RegisterModuleRequest request,
        CancellationToken ct)
    {
        var registration = await _registry.RegisterAsync(request, ct).ConfigureAwait(false);
        _logger.LogInformation("Module registered: {Name} ({Id})", registration.ModuleName, registration.ModuleId);
        return Ok(registration);
    }

    /// <summary>Deregister a module by ID.</summary>
    /// <response code="204">Deregistered successfully.</response>
    [HttpDelete("{moduleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeregisterAsync(Guid moduleId, CancellationToken ct)
    {
        await _registry.DeregisterAsync(moduleId, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>List all currently registered modules.</summary>
    /// <response code="200">List of module registrations.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ModuleRegistration>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct)
    {
        var modules = await _registry.GetAllAsync(ct).ConfigureAwait(false);
        return Ok(modules);
    }
}

/// <summary>Health check controller.</summary>
[ApiController]
[Route("")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns 200 OK with a status object. Used for container health checks.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new { status = "healthy", module = "block-controller", timestamp = DateTimeOffset.UtcNow });
}
