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
/// Semantic Kernel plugin that provides analytics and visualisation capabilities:
/// live price charts, SHAP feature importance plots, and performance reports.
/// </summary>
public sealed class AnalyticsPlugin(
    IHttpClientFactory _httpFactory,
    IOptions<AIHubOptions> _options,
    ICanvasActionDispatcher _canvasDispatcher,
    ILogger<AnalyticsPlugin> _logger)
{
    /// <summary>
    /// Open a live price chart for a symbol on the canvas MDI panel.
    /// Emits an AI_CANVAS_ACTION(OpenPanel) envelope before returning.
    /// </summary>
    [KernelFunction, Description("Open a live price/candlestick chart for a trading symbol on the canvas")]
    public async Task<string> PlotChart(
        [Description("Authenticated user identifier used to route the chart panel to the correct canvas")] Guid userId,
        [Description("Trading symbol to chart (e.g. 'BTC-PERP', 'ETH-PERP')")] string symbol,
        [Description("Timeframe: '1m', '5m', '15m', '1h', '4h', '1d'. Default is '1h'.")] string timeframe = "1h",
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return "A valid userId is required to open the chart on the canvas.";
        var panelData = JsonSerializer.SerializeToElement(new { symbol, timeframe });

        // Dispatch canvas action BEFORE returning text (opens panel in parallel)
        await _canvasDispatcher.DispatchAsync(
            new OpenPanelAction("TradingChart", panelData, $"{symbol} {timeframe.ToUpperInvariant()}"),
            userId, ct).ConfigureAwait(false);

        return $"Opened {symbol} {timeframe.ToUpperInvariant()} candlestick chart on your canvas. Fetching live data...";
    }

    /// <summary>
    /// Generate a SHAP (SHapley Additive exPlanations) feature importance plot for a model
    /// and display it on the canvas.
    /// </summary>
    [KernelFunction, Description("Generate a SHAP feature importance plot for an ML model and display it on the canvas")]
    public async Task<string> GenerateSHAP(
        [Description("Authenticated user identifier used to route the SHAP plot panel to the correct canvas")] Guid userId,
        [Description("Model identifier (e.g. 'model-t', 'model-a', 'model-d') or a full model GUID")] string modelId,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return "A valid userId is required to open the SHAP plot on the canvas.";
        try
        {
            using var client = CreateMlRuntimeClient();
            var response = await client.PostAsJsonAsync(
                $"/api/models/{Uri.EscapeDataString(modelId)}/shap", new { }, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var shapResult = await response.Content.ReadFromJsonAsync<ShapResultDto>(ct).ConfigureAwait(false);
            if (shapResult is null)
                return $"SHAP analysis started for model {modelId}.";

            // Open SHAP plot on canvas
            var panelData = JsonSerializer.SerializeToElement(shapResult);
            await _canvasDispatcher.DispatchAsync(
                new OpenPanelAction("SHAPPlot", panelData, $"SHAP: {modelId}"),
                userId, ct).ConfigureAwait(false);

            var topFeatures = shapResult.TopFeatures.Take(5)
                .Select(f => $"  {f.Feature}: {f.Importance:F4}");

            return $"SHAP analysis for '{modelId}':\n" +
                   $"Top features:\n{string.Join("\n", topFeatures)}\n" +
                   "Full plot opened on canvas.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Model '{modelId}' not found in ML Runtime.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AnalyticsPlugin.GenerateSHAP failed for model {Id}", modelId);
            return $"Failed to generate SHAP plot for model '{modelId}'.";
        }
    }

    /// <summary>
    /// Export a performance report for a strategy or trading session and open it on the canvas.
    /// </summary>
    [KernelFunction, Description("Export a performance analytics report for a strategy or the overall portfolio and display it on the canvas")]
    public async Task<string> ExportReport(
        [Description("Authenticated user identifier used to route the report panel to the correct canvas")] Guid userId,
        [Description("Report type: 'portfolio', 'strategy', or 'model'")] string reportType,
        [Description("Target identifier: strategy ID (GUID) for 'strategy', model ID for 'model', or omit for 'portfolio'")] string? targetId = null,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return "A valid userId is required to open the report on the canvas.";
        try
        {
            using var client = CreateTraderClient();
            var path = reportType.ToLowerInvariant() switch
            {
                "strategy" when targetId is not null => $"/api/reports/strategy/{Uri.EscapeDataString(targetId)}",
                "model"    when targetId is not null => $"/api/reports/model/{Uri.EscapeDataString(targetId)}",
                _                                    => "/api/reports/portfolio",
            };

            var reportResponse = await client.GetAsync(path, ct).ConfigureAwait(false);
            reportResponse.EnsureSuccessStatusCode();

            var reportData = await reportResponse.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);

            // Open report panel on canvas
            await _canvasDispatcher.DispatchAsync(
                new OpenPanelAction("AnalyticsReport", reportData,
                    $"{Capitalize(reportType)} Report{(targetId is not null ? $": {targetId}" : "")}"),
                userId, ct).ConfigureAwait(false);

            return $"{Capitalize(reportType)} report opened on canvas.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AnalyticsPlugin.ExportReport failed for type {Type}", reportType);
            return $"Failed to generate {reportType} report.";
        }
    }

    /// <summary>
    /// Answer a specific data question about chart patterns, indicators, or performance metrics
    /// by querying the analytics data layer directly.
    /// </summary>
    [KernelFunction, Description("Answer a data question by querying recent price data, indicators, or performance metrics for a symbol")]
    public async Task<string> AskAboutData(
        [Description("The data question or analytical query (e.g. 'What is the RSI for BTC over the last hour?')")] string question,
        [Description("Symbol to focus the query on (e.g. 'BTC-PERP')")] string? symbol = null,
        CancellationToken ct = default)
    {
        try
        {
            using var client = CreateTraderClient();
            var request = new { question, symbol };
            var response = await client.PostAsJsonAsync("/api/analytics/ask", request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DataQueryResultDto>(ct).ConfigureAwait(false);
            return result?.Answer ?? "No data answer available.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AnalyticsPlugin.AskAboutData failed");
            return "Unable to answer data query — analytics service may be unavailable.";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateMlRuntimeClient()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.MlRuntimeUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private HttpClient CreateTraderClient()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.TraderUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record ShapResultDto(
        [property: JsonPropertyName("model_id")]     string ModelId,
        [property: JsonPropertyName("top_features")] IReadOnlyList<ShapFeatureDto> TopFeatures);

    private sealed record ShapFeatureDto(
        [property: JsonPropertyName("feature")]    string Feature,
        [property: JsonPropertyName("importance")] float Importance);

    private sealed record DataQueryResultDto(
        [property: JsonPropertyName("answer")] string Answer);
}
