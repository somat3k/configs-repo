using Microsoft.AspNetCore.Mvc;

namespace MLS.Network.UniqueIdGenerator.Controllers;

/// <summary>REST controller for unique ID generation endpoints.</summary>
[ApiController]
[Route("api/id")]
public sealed class UniqueIdGeneratorController(IUniqueIdService _service) : ControllerBase
{
    /// <summary>Generates a new UUID (32-char hex).</summary>
    [HttpGet("uuid")]
    public IActionResult GetUuid() => Ok(new { id = _service.GenerateUuid() });

    /// <summary>Generates the next sequential ID for the given prefix.</summary>
    [HttpGet("sequential/{prefix}")]
    public IActionResult GetSequential(string prefix) =>
        Ok(new { prefix, id = _service.GenerateSequentialId(prefix) });

    /// <summary>Streams <paramref name="count"/> UUIDs as a JSON array.</summary>
    [HttpGet("stream")]
    public async Task<IActionResult> StreamUuids([FromQuery] int count = 10)
    {
        var ids = new List<string>();
        await foreach (var id in _service.StreamUuidsAsync(count, HttpContext.RequestAborted)
            .ConfigureAwait(false))
        {
            ids.Add(id);
        }
        return Ok(ids);
    }
}
