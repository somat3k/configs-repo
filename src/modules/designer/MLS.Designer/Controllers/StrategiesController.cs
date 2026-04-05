using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using MLS.Designer.Persistence;
using MLS.Designer.Services;

namespace MLS.Designer.Controllers;

/// <summary>
/// REST API for strategy graph persistence, template management, and lifecycle operations.
/// </summary>
[ApiController]
[Route("api/strategies")]
public sealed class StrategiesController(
    StrategyRepository _repo,
    IEnvelopeSender _envelopeSender,
    ILogger<StrategiesController> _logger) : ControllerBase
{
    // ── List / Get ────────────────────────────────────────────────────────────────

    /// <summary>List all strategies (name, type, status — no full graph payload).</summary>
    /// <response code="200">List of strategy summaries.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StrategySummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var summaries = await _repo.ListAsync(ct).ConfigureAwait(false);
        return Ok(summaries);
    }

    /// <summary>Get the full strategy schema JSON for a specific strategy.</summary>
    /// <param name="id">Strategy graph ID (UUID).</param>
    /// <response code="200">Strategy schema.</response>
    /// <response code="404">Strategy not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<StrategySchema>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var schema = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (schema is null)
            return NotFound(new { error = $"Strategy '{id}' not found." });

        return Ok(schema);
    }

    // ── Create / Update / Delete ──────────────────────────────────────────────────

    /// <summary>Create a new strategy from a graph schema payload.</summary>
    /// <response code="201">Strategy created successfully.</response>
    /// <response code="400">Invalid payload or duplicate graph ID.</response>
    [HttpPost]
    [ProducesResponseType<StrategySchema>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateStrategyRequest request, CancellationToken ct)
    {
        if (request.Graph is null)
            return BadRequest(new { error = "Graph payload is required." });

        try
        {
            var entity = await _repo.CreateAsync(
                request.Graph,
                request.StrategyType ?? "trading",
                request.Description,
                request.TemplateName,
                ct).ConfigureAwait(false);

            return CreatedAtAction(nameof(GetById), new { id = entity.GraphId }, entity);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Duplicate strategy creation attempt for {GraphId}", request.Graph.GraphId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Update an existing strategy schema (increments SchemaVersion).</summary>
    /// <param name="id">Strategy graph ID.</param>
    /// <response code="200">Updated strategy schema.</response>
    /// <response code="404">Strategy not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<StrategySchema>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] StrategyGraphPayload graph, CancellationToken ct)
    {
        var updated = await _repo.UpdateAsync(id, graph, ct).ConfigureAwait(false);
        if (updated is null)
            return NotFound(new { error = $"Strategy '{id}' not found." });

        return Ok(updated);
    }

    /// <summary>Soft-delete a strategy.</summary>
    /// <param name="id">Strategy graph ID.</param>
    /// <response code="204">Strategy deleted.</response>
    /// <response code="404">Strategy not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _repo.SoftDeleteAsync(id, ct).ConfigureAwait(false);
        if (!deleted)
            return NotFound(new { error = $"Strategy '{id}' not found." });

        return NoContent();
    }

    // ── Lifecycle operations ──────────────────────────────────────────────────────

    /// <summary>Deploy a strategy by emitting STRATEGY_DEPLOY to Block Controller.</summary>
    /// <param name="id">Strategy graph ID.</param>
    /// <response code="202">Deploy request accepted.</response>
    /// <response code="404">Strategy not found.</response>
    [HttpPost("{id:guid}/deploy")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deploy(Guid id, CancellationToken ct)
    {
        var schema = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (schema is null)
            return NotFound(new { error = $"Strategy '{id}' not found." });

        var graph = JsonSerializer.Deserialize<StrategyGraphPayload>(schema.GraphJson);
        if (graph is null)
            return BadRequest(new { error = "Could not deserialise strategy graph." });

        await _envelopeSender.SendEnvelopeAsync(
            EnvelopePayload.Create(MessageTypes.StrategyDeploy, "designer", graph), ct)
            .ConfigureAwait(false);

        await _repo.SetStatusAsync(id, "deployed", ct).ConfigureAwait(false);

        _logger.LogInformation("Deployed strategy {GraphId}", id);
        return Accepted(new { message = "Strategy deploy request sent.", graph_id = id });
    }

    /// <summary>Stop a deployed strategy.</summary>
    /// <param name="id">Strategy graph ID.</param>
    /// <response code="202">Stop request accepted.</response>
    /// <response code="404">Strategy not found.</response>
    [HttpPost("{id:guid}/stop")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        var schema = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (schema is null)
            return NotFound(new { error = $"Strategy '{id}' not found." });

        var stateChange = new StrategyStateChangePayload(id, schema.Status, "stopped", DateTimeOffset.UtcNow);
        await _envelopeSender.SendEnvelopeAsync(
            EnvelopePayload.Create(MessageTypes.StrategyStateChange, "designer", stateChange), ct)
            .ConfigureAwait(false);

        await _repo.SetStatusAsync(id, "stopped", ct).ConfigureAwait(false);
        return Accepted(new { message = "Strategy stop request sent.", graph_id = id });
    }

    /// <summary>Start a backtest run for a strategy.</summary>
    /// <param name="id">Strategy graph ID.</param>
    /// <param name="request">Backtest date range parameters.</param>
    /// <response code="202">Backtest request accepted.</response>
    /// <response code="404">Strategy not found.</response>
    [HttpPost("{id:guid}/backtest")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Backtest(
        Guid id,
        [FromBody] BacktestRequest request,
        CancellationToken ct)
    {
        var schema = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (schema is null)
            return NotFound(new { error = $"Strategy '{id}' not found." });

        var stateChange = new StrategyStateChangePayload(
            id, schema.Status, "backtesting", DateTimeOffset.UtcNow);

        await _envelopeSender.SendEnvelopeAsync(
            EnvelopePayload.Create(MessageTypes.StrategyStateChange, "designer", stateChange), ct)
            .ConfigureAwait(false);

        await _repo.SetStatusAsync(id, "backtesting", ct).ConfigureAwait(false);
        return Accepted(new { message = "Backtest request sent.", graph_id = id });
    }

    // ── Templates ─────────────────────────────────────────────────────────────────

    /// <summary>List all available pre-built strategy templates.</summary>
    /// <response code="200">List of template descriptors.</response>
    [HttpGet("/api/templates")]
    [ProducesResponseType<IReadOnlyList<TemplateInfo>>(StatusCodes.Status200OK)]
    public IActionResult ListTemplates()
    {
        var templates = _repo.ListTemplates();
        return Ok(templates);
    }

    /// <summary>Create a new strategy from a named template.</summary>
    /// <param name="name">Template name (file name without extension).</param>
    /// <param name="strategyName">Display name for the new strategy.</param>
    /// <response code="201">Strategy created from template.</response>
    /// <response code="404">Template not found.</response>
    [HttpPost("/api/strategies/from-template/{name}")]
    [ProducesResponseType<StrategySchema>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateFromTemplate(
        string name,
        [FromQuery] string strategyName = "",
        CancellationToken ct = default)
    {
        var template = await _repo.LoadTemplateAsync(name, ct).ConfigureAwait(false);
        if (template is null)
            return NotFound(new { error = $"Template '{name}' not found." });

        // Assign a new graph ID so the instance is independent from the template
        var newGraph = template with
        {
            GraphId = Guid.NewGuid(),
            Name    = string.IsNullOrWhiteSpace(strategyName) ? $"{template.Name} (copy)" : strategyName,
            SchemaVersion = 1,
        };

        var entity = await _repo.CreateAsync(newGraph, templateName: name, ct: ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = entity.GraphId }, entity);
    }
}

// ── Request / response DTOs ────────────────────────────────────────────────────

/// <summary>Request body for <c>POST /api/strategies</c>.</summary>
/// <param name="Graph">The strategy graph payload to persist.</param>
/// <param name="StrategyType">Category: <c>trading</c>, <c>arbitrage</c>, <c>defi</c>, <c>ml-training</c>.</param>
/// <param name="Description">Optional description.</param>
/// <param name="TemplateName">Template this strategy was derived from, if any.</param>
public sealed record CreateStrategyRequest(
    StrategyGraphPayload? Graph,
    string? StrategyType,
    string? Description,
    string? TemplateName);

/// <summary>Request body for backtest lifecycle call.</summary>
/// <param name="From">Backtest start date (UTC).</param>
/// <param name="To">Backtest end date (UTC).</param>
public sealed record BacktestRequest(DateTimeOffset From, DateTimeOffset To);
