using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLS.Core.Contracts.Designer;

namespace MLS.Designer.Persistence;

/// <summary>
/// EF Core repository for <see cref="StrategySchema"/> entities.
/// Provides full CRUD operations and template-based creation for strategy graphs.
/// </summary>
public sealed class StrategyRepository(
    DesignerDbContext _db,
    ILogger<StrategyRepository> _logger)
{
    // ── Read operations ───────────────────────────────────────────────────────────

    /// <summary>
    /// List all non-deleted strategies with summary fields (no <c>graph_json</c> payload).
    /// </summary>
    public async Task<IReadOnlyList<StrategySummary>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Strategies
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new StrategySummary(
                s.GraphId, s.Name, s.StrategyType, s.Status,
                s.SchemaVersion, s.TemplateName, s.CreatedAt, s.UpdatedAt))
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Get a single strategy by its graph ID, including the full <c>graph_json</c>.
    /// Returns <see langword="null"/> when the strategy does not exist or is soft-deleted.
    /// </summary>
    public async Task<StrategySchema?> GetByIdAsync(Guid graphId, CancellationToken ct = default)
    {
        return await _db.Strategies
            .Where(s => s.GraphId == graphId && !s.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    // ── Write operations ──────────────────────────────────────────────────────────

    /// <summary>
    /// Persist a new strategy graph schema.
    /// Returns the saved entity after committing to the database.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a strategy with the same <paramref name="graph"/> graph ID already exists.
    /// </exception>
    public async Task<StrategySchema> CreateAsync(
        StrategyGraphPayload graph,
        string strategyType = "trading",
        string? description = null,
        string? templateName = null,
        CancellationToken ct = default)
    {
        var entity = new StrategySchema
        {
            GraphId      = graph.GraphId,
            Name         = graph.Name,
            StrategyType = strategyType,
            Status       = "draft",
            SchemaVersion = graph.SchemaVersion,
            GraphJson    = JsonSerializer.Serialize(graph),
            Description  = description,
            TemplateName = templateName,
            CreatedAt    = DateTimeOffset.UtcNow,
            UpdatedAt    = DateTimeOffset.UtcNow,
        };

        _db.Strategies.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to create strategy {GraphId}", graph.GraphId);
            throw new InvalidOperationException(
                $"Strategy with graph_id '{graph.GraphId}' already exists.", ex);
        }

        _logger.LogInformation("Created strategy {GraphId} ({Name})", entity.GraphId, entity.Name);
        return entity;
    }

    /// <summary>
    /// Update an existing strategy schema.  Increments <c>SchemaVersion</c> automatically.
    /// Returns the updated entity, or <see langword="null"/> when the strategy is not found.
    /// </summary>
    public async Task<StrategySchema?> UpdateAsync(
        Guid graphId,
        StrategyGraphPayload graph,
        CancellationToken ct = default)
    {
        var entity = await _db.Strategies
            .Where(s => s.GraphId == graphId && !s.IsDeleted)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (entity is null) return null;

        entity.Name          = graph.Name;
        entity.SchemaVersion = entity.SchemaVersion + 1;
        entity.GraphJson     = JsonSerializer.Serialize(graph with { SchemaVersion = entity.SchemaVersion });
        entity.UpdatedAt     = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Updated strategy {GraphId} to v{Version}", graphId, entity.SchemaVersion);
        return entity;
    }

    /// <summary>
    /// Soft-delete a strategy by setting <c>is_deleted = true</c>.
    /// Returns <see langword="true"/> on success, <see langword="false"/> when not found.
    /// </summary>
    public async Task<bool> SoftDeleteAsync(Guid graphId, CancellationToken ct = default)
    {
        var entity = await _db.Strategies
            .Where(s => s.GraphId == graphId && !s.IsDeleted)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (entity is null) return false;

        entity.IsDeleted  = true;
        entity.UpdatedAt  = DateTimeOffset.UtcNow;
        entity.Status     = "stopped";

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Soft-deleted strategy {GraphId}", graphId);
        return true;
    }

    /// <summary>
    /// Update the deployment status of a strategy (e.g. <c>deployed</c>, <c>stopped</c>, <c>backtesting</c>).
    /// Returns the updated entity, or <see langword="null"/> when not found.
    /// </summary>
    public async Task<StrategySchema?> SetStatusAsync(
        Guid graphId, string status, CancellationToken ct = default)
    {
        var entity = await _db.Strategies
            .Where(s => s.GraphId == graphId && !s.IsDeleted)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (entity is null) return null;

        entity.Status    = status;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return entity;
    }

    // ── Template support ──────────────────────────────────────────────────────────

    /// <summary>
    /// List all available strategy templates by scanning the <c>designer-templates/</c>
    /// directory (relative to the application base directory).
    /// </summary>
    public IReadOnlyList<TemplateInfo> ListTemplates()
    {
        var baseDir = AppContext.BaseDirectory;
        var templatesDir = Path.Combine(baseDir, "designer-templates");

        if (!Directory.Exists(templatesDir))
            return Array.Empty<TemplateInfo>();

        return Directory.GetFiles(templatesDir, "*.json", SearchOption.AllDirectories)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(templatesDir, path);
                var category = Path.GetDirectoryName(relative)?.Replace(Path.DirectorySeparatorChar, '/') ?? "other";
                var name     = Path.GetFileNameWithoutExtension(path);
                return new TemplateInfo(name, category, relative);
            })
            .OrderBy(t => t.Category).ThenBy(t => t.Name)
            .ToList();
    }

    /// <summary>
    /// Load a strategy template by name and return it as a <see cref="StrategyGraphPayload"/>.
    /// Returns <see langword="null"/> when the template file is not found.
    /// </summary>
    public async Task<StrategyGraphPayload?> LoadTemplateAsync(
        string templateName, CancellationToken ct = default)
    {
        var baseDir      = AppContext.BaseDirectory;
        var templatesDir = Path.Combine(baseDir, "designer-templates");

        if (!Directory.Exists(templatesDir)) return null;

        var files = Directory.GetFiles(templatesDir, $"{templateName}.json", SearchOption.AllDirectories);
        if (files.Length == 0) return null;

        var json = await File.ReadAllTextAsync(files[0], ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<StrategyGraphPayload>(json);
    }
}

/// <summary>Lightweight summary of a strategy (no graph_json payload).</summary>
/// <param name="GraphId">Strategy graph identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="StrategyType">Category (trading/arbitrage/defi/ml-training).</param>
/// <param name="Status">Deployment status.</param>
/// <param name="SchemaVersion">Current schema version.</param>
/// <param name="TemplateName">Source template, if any.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="UpdatedAt">Last update timestamp.</param>
public sealed record StrategySummary(
    Guid GraphId,
    string Name,
    string StrategyType,
    string Status,
    int SchemaVersion,
    string? TemplateName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Template descriptor returned by the templates list endpoint.</summary>
/// <param name="Name">File name without extension.</param>
/// <param name="Category">Sub-directory category (e.g. <c>trading</c>, <c>arbitrage</c>).</param>
/// <param name="RelativePath">Path relative to the templates root directory.</param>
public sealed record TemplateInfo(string Name, string Category, string RelativePath);
