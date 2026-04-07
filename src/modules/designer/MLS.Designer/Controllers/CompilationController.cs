using Microsoft.AspNetCore.Mvc;
using MLS.Designer.Compilation;
using MLS.Designer.Persistence;

namespace MLS.Designer.Controllers;

/// <summary>
/// REST API for on-demand Roslyn strategy compilation and IPFS assembly distribution.
/// </summary>
[ApiController]
[Route("api/compile")]
public sealed class CompilationController(
    IStrategyCompiler compiler,
    StrategyRepository repo,
    ILogger<CompilationController> logger) : ControllerBase
{
    // ── POST /api/compile ─────────────────────────────────────────────────────────

    /// <summary>
    /// Compile a C# custom block source and return diagnostics + assembly bytes (Base64).
    /// </summary>
    /// <remarks>
    /// <para>Does NOT upload to IPFS. Use <c>POST /api/compile/upload</c> to compile and publish.</para>
    /// <para>
    /// The compilation target must be a single <c>public sealed class</c> extending
    /// <c>MLS.Designer.Blocks.BlockBase</c>. Forbidden APIs (filesystem, network, reflection)
    /// are rejected before IL emission.
    /// </para>
    /// </remarks>
    /// <response code="200">Compilation succeeded — assembly bytes returned as Base64.</response>
    /// <response code="400">Compilation failed — diagnostics list the errors.</response>
    [HttpPost]
    [ProducesResponseType<CompileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<CompileResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Compile([FromBody] CompileRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Source))
            return BadRequest(new { error = "Source code must not be empty." });

        var result = await compiler.CompileAsync(request.Source, ct).ConfigureAwait(false);

        var response = new CompileResponse(
            result.Success,
            result.AssemblyBytes is not null ? Convert.ToBase64String(result.AssemblyBytes) : null,
            result.Diagnostics,
            null,
            result.ElapsedTime.TotalMilliseconds);

        logger.LogInformation("Compile request: success={Success} in {Elapsed:F0} ms", result.Success, result.ElapsedTime.TotalMilliseconds);

        return result.Success ? Ok(response) : BadRequest(response);
    }

    // ── POST /api/compile/upload ──────────────────────────────────────────────────

    /// <summary>
    /// Compile a C# custom block source, upload the assembly to IPFS, and optionally
    /// store the resulting CID on a strategy schema.
    /// </summary>
    /// <response code="200">Compilation + IPFS upload succeeded.</response>
    /// <response code="400">Compilation failed.</response>
    /// <response code="502">Compilation succeeded but IPFS upload failed.</response>
    [HttpPost("upload")]
    [ProducesResponseType<CompileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<CompileResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<CompileResponse>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CompileAndUpload([FromBody] CompileAndUploadRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Source))
            return BadRequest(new { error = "Source code must not be empty." });

        var result = await compiler.CompileAndUploadAsync(request.Source, ct).ConfigureAwait(false);

        var response = new CompileResponse(
            result.Success,
            result.AssemblyBytes is not null ? Convert.ToBase64String(result.AssemblyBytes) : null,
            result.Diagnostics,
            result.IpfsCid,
            result.ElapsedTime.TotalMilliseconds);

        if (!result.Success)
            return BadRequest(response);

        // Optionally persist the CID back to the strategy schema
        if (!string.IsNullOrWhiteSpace(result.IpfsCid) && request.StrategyId.HasValue)
        {
            var saved = await repo.SetCompiledBlockCidAsync(request.StrategyId.Value, result.IpfsCid, ct)
                .ConfigureAwait(false);
            if (!saved)
                logger.LogWarning("Strategy {StrategyId} not found; CID not persisted", request.StrategyId.Value);
            else
                logger.LogInformation("CID {Cid} persisted on strategy {StrategyId}", result.IpfsCid, request.StrategyId.Value);
        }

        // If upload succeeded entirely return 200; if CID is missing (IPFS unavailable) return 502
        if (result.AssemblyBytes is not null && result.IpfsCid is null)
            return StatusCode(StatusCodes.Status502BadGateway, response with
            {
                Diagnostics = [..response.Diagnostics, "IPFS upload failed — assembly compiled but not published."]
            });

        return Ok(response);
    }
}

// ── Request / response DTOs ────────────────────────────────────────────────────

/// <summary>Request body for <c>POST /api/compile</c>.</summary>
/// <param name="Source">Full C# compilation unit source code.</param>
public sealed record CompileRequest(string Source);

/// <summary>Request body for <c>POST /api/compile/upload</c>.</summary>
/// <param name="Source">Full C# compilation unit source code.</param>
/// <param name="StrategyId">
/// Optional strategy graph ID. When provided and compilation + upload succeed, the
/// resulting IPFS CID is persisted on the strategy's <c>compiled_block_cid</c> column.
/// </param>
public sealed record CompileAndUploadRequest(string Source, Guid? StrategyId);

/// <summary>Response body for all compilation endpoints.</summary>
/// <param name="Success">Whether compilation succeeded.</param>
/// <param name="AssemblyBase64">Base64-encoded DLL bytes, or <see langword="null"/> on failure.</param>
/// <param name="Diagnostics">Compiler messages (errors and warnings).</param>
/// <param name="IpfsCid">IPFS CID of the uploaded assembly, or <see langword="null"/>.</param>
/// <param name="ElapsedMs">Wall-clock compilation duration in milliseconds.</param>
public sealed record CompileResponse(
    bool Success,
    string? AssemblyBase64,
    IReadOnlyList<string> Diagnostics,
    string? IpfsCid,
    double ElapsedMs);
