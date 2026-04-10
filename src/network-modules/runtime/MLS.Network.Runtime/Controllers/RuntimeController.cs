using Microsoft.AspNetCore.Mvc;

namespace MLS.Network.Runtime.Controllers;

/// <summary>REST controller for module runtime management.</summary>
[ApiController]
[Route("api/runtime/modules")]
public sealed class RuntimeController(IModuleRuntimeService _service) : ControllerBase
{
    /// <summary>Lists all MLS-labelled module containers.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var modules = await _service.ListModulesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(modules);
    }

    /// <summary>Gets the status of a named module container.</summary>
    [HttpGet("{name}")]
    public async Task<IActionResult> GetStatus(string name)
    {
        var status = await _service.GetStatusAsync(name, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>Starts a named module container.</summary>
    [HttpPost("{name}/start")]
    public async Task<IActionResult> Start(string name)
    {
        var ok = await _service.StartModuleAsync(name, HttpContext.RequestAborted).ConfigureAwait(false);
        return ok ? Ok(new { started = name }) : BadRequest(new { error = "Could not start module" });
    }

    /// <summary>Stops a named module container.</summary>
    [HttpPost("{name}/stop")]
    public async Task<IActionResult> Stop(string name)
    {
        var ok = await _service.StopModuleAsync(name, HttpContext.RequestAborted).ConfigureAwait(false);
        return ok ? Ok(new { stopped = name }) : BadRequest(new { error = "Could not stop module" });
    }

    /// <summary>Restarts a named module container.</summary>
    [HttpPost("{name}/restart")]
    public async Task<IActionResult> Restart(string name)
    {
        var ok = await _service.RestartModuleAsync(name, HttpContext.RequestAborted).ConfigureAwait(false);
        return ok ? Ok(new { restarted = name }) : BadRequest(new { error = "Could not restart module" });
    }
}
