using Microsoft.AspNetCore.Mvc;
using MLS.MLRuntime.Inference;
using MLS.MLRuntime.Models;

namespace MLS.MLRuntime.Controllers;

/// <summary>
/// HTTP API for the ML Runtime module.
/// Base path: <c>/api/ml-runtime</c>.
/// </summary>
[ApiController]
[Route("api/ml-runtime")]
public sealed class MLRuntimeController(
    IModelRegistry _registry,
    IInferenceEngine _engine,
    ILogger<MLRuntimeController> _logger) : ControllerBase
{
    /// <summary>GET /api/ml-runtime/models — list all loaded models.</summary>
    [HttpGet("models")]
    public ActionResult<IEnumerable<object>> GetModels()
    {
        var models = _registry.Loaded.Values.Select(r => new
        {
            model_key  = r.ModelKey,
            model_id   = r.ModelId,
            model_path = r.ModelPath,
            loaded_at  = r.LoadedAt,
        });
        return Ok(models);
    }

    /// <summary>
    /// POST /api/ml-runtime/models/{modelKey}/reload — load or hot-reload a model from disk.
    /// </summary>
    [HttpPost("models/{modelKey}/reload")]
    public async Task<IActionResult> ReloadModel(
        string modelKey,
        [FromBody] ReloadModelRequest body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Path))
            return BadRequest(new { error = "Path is required." });

        _logger.LogInformation("API: reload model key={Key} path={Path}", modelKey, body.Path);

        try
        {
            await _registry.LoadAsync(modelKey, body.Path, body.ModelId, ct).ConfigureAwait(false);
            return Ok(new { message = "Model loaded.", model_key = modelKey, model_id = body.ModelId });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>DELETE /api/ml-runtime/models/{modelKey} — unload a model.</summary>
    [HttpDelete("models/{modelKey}")]
    public async Task<IActionResult> UnloadModel(string modelKey, CancellationToken ct)
    {
        await _registry.UnloadAsync(modelKey, ct).ConfigureAwait(false);
        return Ok(new { message = "Model unloaded.", model_key = modelKey });
    }

    /// <summary>POST /api/ml-runtime/inference — run a direct HTTP inference call.</summary>
    [HttpPost("inference")]
    public async Task<ActionResult<InferenceResultPayload>> RunInference(
        [FromBody] InferenceRequestPayload request,
        CancellationToken ct)
    {
        try
        {
            var result = await _engine.RunAsync(request, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, new { error = ex.Message });
        }
    }
}

/// <summary>Request body for the model reload endpoint.</summary>
/// <param name="Path">Container-local path to the ONNX file.</param>
/// <param name="ModelId">Optional versioned model identifier.</param>
public sealed record ReloadModelRequest(string Path, string? ModelId = null);
