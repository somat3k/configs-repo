using Microsoft.AspNetCore.Mvc;

namespace MLS.Network.TaskIdGenerator.Controllers;

/// <summary>Request body for task ID generation.</summary>
public sealed record GenerateTaskIdRequest(string module_id, string task_type);

/// <summary>Request body for task ID validation.</summary>
public sealed record ValidateTaskIdRequest(string task_id);

/// <summary>REST controller for task ID generation and validation.</summary>
[ApiController]
[Route("api/task-id")]
public sealed class TaskIdGeneratorController(ITaskIdService _service) : ControllerBase
{
    /// <summary>Generates a new task ID.</summary>
    [HttpPost]
    public IActionResult Generate([FromBody] GenerateTaskIdRequest request)
    {
        var id = _service.GenerateTaskId(request.module_id, request.task_type);
        return Ok(new { task_id = id });
    }

    /// <summary>Validates a task ID and parses its components.</summary>
    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ValidateTaskIdRequest request)
    {
        var valid      = _service.ValidateTaskId(request.task_id);
        var components = _service.ParseTaskId(request.task_id);
        return Ok(new { valid, components });
    }
}
