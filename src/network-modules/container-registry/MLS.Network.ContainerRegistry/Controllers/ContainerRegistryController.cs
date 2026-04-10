using Microsoft.AspNetCore.Mvc;

namespace MLS.Network.ContainerRegistry.Controllers;

/// <summary>REST controller for container image registry endpoints.</summary>
[ApiController]
[Route("api/registry/images")]
public sealed class ContainerRegistryController(IContainerRegistryService _service) : ControllerBase
{
    /// <summary>Lists all registered container images.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var images = new List<ContainerImage>();
        await foreach (var img in _service.ListImagesAsync(HttpContext.RequestAborted))
            images.Add(img);
        return Ok(images);
    }

    /// <summary>Registers a new container image.</summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterImageRequest request)
    {
        var image = await _service.RegisterImageAsync(request, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(image);
    }

    /// <summary>Gets a container image by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var image = await _service.GetImageAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);
        return image is null ? NotFound() : Ok(image);
    }

    /// <summary>Removes a container image registration.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id)
    {
        var removed = await _service.RemoveImageAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>Records a health check result for an image.</summary>
    [HttpPost("{id:guid}/health")]
    public async Task<IActionResult> RecordHealth(Guid id, [FromBody] HealthCheckResult result)
    {
        if (result.ImageId != id)
            return BadRequest(new { error = "The image ID in the request body must match the image ID in the route." });

        await _service.RecordHealthCheckAsync(id, result, HttpContext.RequestAborted).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Returns health check history for an image.</summary>
    [HttpGet("{id:guid}/health")]
    public async Task<IActionResult> GetHealth(Guid id, [FromQuery] int limit = 20)
    {
        var history = await _service.GetHealthHistoryAsync(id, limit, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(history);
    }
}
