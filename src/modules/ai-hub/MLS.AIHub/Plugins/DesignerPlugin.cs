using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using MLS.AIHub.Canvas;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Plugins;

/// <summary>
/// Semantic Kernel plugin that exposes Designer module operations to the AI:
/// strategy creation, block graph manipulation, backtest execution, and explanation.
/// </summary>
public sealed class DesignerPlugin(
    IHttpClientFactory _httpFactory,
    IOptions<AIHubOptions> _options,
    ICanvasActionDispatcher _canvasDispatcher,
    ILogger<DesignerPlugin> _logger)
{
    /// <summary>
    /// Create a new trading strategy from a named template and persist it in the Designer.
    /// Returns the new strategy ID and a summary of the template blocks.
    /// </summary>
    [KernelFunction, Description("Create a new trading strategy from a predefined template and save it in the Designer module")]
    public async Task<string> CreateStrategy(
        [Description("Authenticated user identifier used to route the canvas graph action to the correct SignalR group")] Guid userId,
        [Description("Display name for the new strategy (e.g. 'BTC RSI Crossover')")] string name,
        [Description("Template name to use as the starting graph (e.g. 'rsi-crossover', 'ma-trend', 'arb-simple')")] string templateName,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return "A valid userId is required to open the strategy graph on the canvas.";

        try
        {
            using var client = CreateDesignerClient();
            var request = new { name, template_name = templateName };
            var response = await client.PostAsJsonAsync("/api/strategies/from-template", request, ct)
                                       .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var strategy = await response.Content
                                         .ReadFromJsonAsync<StrategyDto>(ct)
                                         .ConfigureAwait(false);
            if (strategy is null)
                return "Strategy created but no details returned.";

            // Open the strategy graph on the canvas
            var schemaElement = JsonSerializer.SerializeToElement(strategy);
            await _canvasDispatcher.DispatchAsync(
                new OpenDesignerGraphAction(schemaElement), userId, ct).ConfigureAwait(false);

            return $"Strategy '{strategy.Name}' created (ID: {strategy.StrategyId}). " +
                   $"Template: {templateName} | Blocks: {strategy.BlockCount}. " +
                   "The strategy graph has been opened on your canvas.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DesignerPlugin.CreateStrategy failed");
            return $"Failed to create strategy '{name}' — Designer module may be unavailable.";
        }
    }

    /// <summary>
    /// Add a block of the specified type to the currently active strategy canvas.
    /// The block is initialised with the provided JSON parameter overrides.
    /// </summary>
    [KernelFunction, Description("Add a block to the active strategy canvas. Provide block type and optional JSON parameter overrides.")]
    public async Task<string> AddBlock(
        [Description("Block type identifier (e.g. 'RSIBlock', 'TrainModelBlock', 'MorphoSupplyBlock')")] string blockType,
        [Description("JSON object with parameter overrides (e.g. '{\"period\": 14}'). Use '{}' for defaults.")] string jsonParameters,
        CancellationToken ct = default)
    {
        try
        {
            JsonElement parametersElement;
            try
            {
                parametersElement = JsonSerializer.Deserialize<JsonElement>(jsonParameters);
            }
            catch (JsonException)
            {
                return $"Invalid JSON for parameters: {jsonParameters}";
            }

            using var client = CreateDesignerClient();
            var request = new { block_type = blockType, parameters = parametersElement };
            var response = await client.PostAsJsonAsync("/api/strategies/active/blocks", request, ct)
                                       .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var block = await response.Content.ReadFromJsonAsync<BlockAddedDto>(ct).ConfigureAwait(false);
            return block is null
                ? $"Block '{blockType}' added to the active strategy."
                : $"Block '{blockType}' added (Block ID: {block.BlockId}). " +
                  $"Strategy now has {block.TotalBlocks} block(s).";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return "No active strategy on the canvas. Use CreateStrategy first.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DesignerPlugin.AddBlock failed for block type {Type}", blockType);
            return $"Failed to add block '{blockType}' — Designer module may be unavailable.";
        }
    }

    /// <summary>
    /// Run a backtest on a specific strategy over a date range.
    /// Dispatches a canvas action to open the backtest results panel when complete.
    /// </summary>
    [KernelFunction, Description("Run a backtest on a strategy and display the results on the canvas")]
    public async Task<string> RunBacktest(
        [Description("Authenticated user identifier used to route the backtest results panel to the correct canvas")] Guid userId,
        [Description("Strategy unique identifier (GUID)")] Guid strategyId,
        [Description("Backtest start date in ISO 8601 format (e.g. '2024-01-01T00:00:00Z')")] DateTimeOffset from,
        [Description("Backtest end date in ISO 8601 format (e.g. '2024-12-31T00:00:00Z')")] DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (from >= to)
            return "Backtest 'from' date must be before 'to' date.";

        try
        {
            using var client = CreateDesignerClient();
            var request = new { from, to };
            var response = await client
                .PostAsJsonAsync($"/api/strategies/{strategyId}/backtest", request, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BacktestResultDto>(ct).ConfigureAwait(false);
            if (result is null)
                return $"Backtest started for strategy {strategyId}.";

            // Open the backtest results panel on the canvas
            var resultElement = JsonSerializer.SerializeToElement(result);
            await _canvasDispatcher.DispatchAsync(
                new OpenPanelAction("BacktestResults", resultElement, $"Backtest: {result.StrategyName}"),
                userId, ct).ConfigureAwait(false);

            return $"Backtest complete for '{result.StrategyName}':\n" +
                   $"  Total return: {result.TotalReturn:+0.00;-0.00}%\n" +
                   $"  Sharpe ratio: {result.SharpeRatio:F2}\n" +
                   $"  Max drawdown: {result.MaxDrawdown:F2}%\n" +
                   $"  Win rate: {result.WinRate * 100:F0}% ({result.WinningTrades}/{result.TotalTrades} trades)\n" +
                   "Results panel opened on canvas.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Strategy {strategyId} not found.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DesignerPlugin.RunBacktest failed for strategy {Id}", strategyId);
            return $"Failed to run backtest for strategy {strategyId}.";
        }
    }

    /// <summary>
    /// Explain what a strategy does: list its blocks, connections, and describe its trading logic.
    /// </summary>
    [KernelFunction, Description("Explain the trading logic of a strategy by describing its blocks, connections, and data flow")]
    public async Task<string> ExplainStrategy(
        [Description("Strategy unique identifier (GUID), or omit to explain the currently active strategy")] Guid? strategyId = null,
        CancellationToken ct = default)
    {
        try
        {
            using var client = CreateDesignerClient();
            var path = strategyId.HasValue
                ? $"/api/strategies/{strategyId.Value}"
                : "/api/strategies/active";

            var strategy = await client.GetFromJsonAsync<StrategyDetailDto>(path, ct).ConfigureAwait(false);
            if (strategy is null)
                return "Strategy not found.";

            var blockList = strategy.Blocks.Count > 0
                ? string.Join(", ", strategy.Blocks.Select(b => b.BlockType))
                : "no blocks";

            return $"Strategy: '{strategy.Name}' (ID: {strategy.StrategyId})\n" +
                   $"State: {strategy.State} | Blocks: {strategy.Blocks.Count} | Connections: {strategy.ConnectionCount}\n" +
                   $"Block types: {blockList}\n" +
                   $"Description: {strategy.Description ?? "No description available."}";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return "Strategy not found or no active strategy on canvas.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DesignerPlugin.ExplainStrategy failed");
            return "Failed to retrieve strategy details — Designer module may be unavailable.";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateDesignerClient()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.DesignerUrl);
        client.Timeout = TimeSpan.FromSeconds(30); // backtests may take longer
        return client;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record StrategyDto(
        [property: JsonPropertyName("strategy_id")] Guid StrategyId,
        [property: JsonPropertyName("name")]         string Name,
        [property: JsonPropertyName("state")]        string State,
        [property: JsonPropertyName("block_count")]  int BlockCount);

    private sealed record BlockAddedDto(
        [property: JsonPropertyName("block_id")]     Guid BlockId,
        [property: JsonPropertyName("total_blocks")] int TotalBlocks);

    private sealed record BacktestResultDto(
        [property: JsonPropertyName("strategy_id")]   Guid StrategyId,
        [property: JsonPropertyName("strategy_name")] string StrategyName,
        [property: JsonPropertyName("total_return")]  decimal TotalReturn,
        [property: JsonPropertyName("sharpe_ratio")]  decimal SharpeRatio,
        [property: JsonPropertyName("max_drawdown")]  decimal MaxDrawdown,
        [property: JsonPropertyName("win_rate")]      float WinRate,
        [property: JsonPropertyName("winning_trades")] int WinningTrades,
        [property: JsonPropertyName("total_trades")]  int TotalTrades);

    private sealed record StrategyDetailDto(
        [property: JsonPropertyName("strategy_id")]     Guid StrategyId,
        [property: JsonPropertyName("name")]            string Name,
        [property: JsonPropertyName("state")]           string State,
        [property: JsonPropertyName("description")]     string? Description,
        [property: JsonPropertyName("connection_count")] int ConnectionCount,
        [property: JsonPropertyName("blocks")]          IReadOnlyList<BlockSummaryDto> Blocks);

    private sealed record BlockSummaryDto(
        [property: JsonPropertyName("block_id")]   Guid BlockId,
        [property: JsonPropertyName("block_type")] string BlockType);
}
