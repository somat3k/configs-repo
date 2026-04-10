using Microsoft.AspNetCore.Mvc;

namespace MLS.Network.VirtualMachine.Controllers;

/// <summary>REST controller for virtual machine sandbox execution.</summary>
[ApiController]
[Route("api/vm")]
public sealed class VirtualMachineController(IVirtualMachineService _service) : ControllerBase
{
    /// <summary>Executes a C# script in a sandboxed environment.</summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] SandboxRequest request)
    {
        var result = await _service.ExecuteAsync(request, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Lists all sandbox instances.</summary>
    [HttpGet("sandboxes")]
    public async Task<IActionResult> ListSandboxes()
    {
        var sandboxes = new List<SandboxInfo>();
        await foreach (var s in _service.GetActiveSandboxesAsync(HttpContext.RequestAborted))
            sandboxes.Add(s);
        return Ok(sandboxes);
    }

    /// <summary>Gets a sandbox instance by ID.</summary>
    [HttpGet("sandboxes/{id:guid}")]
    public async Task<IActionResult> GetSandbox(Guid id)
    {
        var sandbox = await _service.GetSandboxAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);
        return sandbox is null ? NotFound() : Ok(sandbox);
    }

    /// <summary>Terminates and removes a sandbox by ID.</summary>
    [HttpDelete("sandboxes/{id:guid}")]
    public async Task<IActionResult> TerminateSandbox(Guid id)
    {
        await _service.TerminateSandboxAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);
        return NoContent();
    }
}
