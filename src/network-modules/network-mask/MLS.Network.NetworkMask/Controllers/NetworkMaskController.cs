using Microsoft.AspNetCore.Mvc;

namespace MLS.Network.NetworkMask.Controllers;

/// <summary>REST controller for endpoint registry and resolution.</summary>
[ApiController]
[Route("api/endpoints")]
public sealed class NetworkMaskController(INetworkMaskService _service) : ControllerBase
{
    /// <summary>Lists all registered endpoints.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var endpoints = new List<EndpointInfo>();
        await foreach (var ep in _service.ListEndpointsAsync(HttpContext.RequestAborted))
            endpoints.Add(ep);
        return Ok(endpoints);
    }

    /// <summary>Registers a new endpoint.</summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] EndpointRegistration request)
    {
        await _service.RegisterEndpointAsync(request, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(new { registered = true });
    }

    /// <summary>Resolves an endpoint for a module in the given environment.</summary>
    [HttpGet("{module}/{env}")]
    public async Task<IActionResult> Resolve(string module, string env)
    {
        var info = await _service.ResolveEndpointAsync(module, env, HttpContext.RequestAborted).ConfigureAwait(false);
        return info is null ? NotFound() : Ok(info);
    }

    /// <summary>Removes an endpoint registration.</summary>
    [HttpDelete("{module}/{env}")]
    public async Task<IActionResult> Remove(string module, string env)
    {
        var removed = await _service.RemoveEndpointAsync(module, env, HttpContext.RequestAborted).ConfigureAwait(false);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>Resolves the full URL for a path on a module endpoint.</summary>
    [HttpGet("{module}/{env}/resolve")]
    public async Task<IActionResult> ResolveUrl(string module, string env, [FromQuery] string path = "/health")
    {
        var url = await _service.ResolveUrlAsync(module, env, path, HttpContext.RequestAborted).ConfigureAwait(false);
        return url is null ? NotFound() : Ok(new { url });
    }
}
