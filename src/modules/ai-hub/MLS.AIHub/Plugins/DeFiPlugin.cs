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
/// Semantic Kernel plugin that exposes DeFi operations to the AI:
/// health factor monitoring (Morpho), pool APY queries (Balancer),
/// rebalancing simulations, and liquidity position management.
/// </summary>
public sealed class DeFiPlugin(
    IHttpClientFactory _httpFactory,
    IOptions<AIHubOptions> _options,
    ICanvasActionDispatcher _canvasDispatcher,
    ILogger<DeFiPlugin> _logger)
{
    /// <summary>User context injected for canvas action routing.</summary>
    internal Guid UserId { get; set; }

    /// <summary>
    /// Get the health factors for all open DeFi positions (Morpho, Balancer).
    /// Highlights any positions at risk of liquidation.
    /// </summary>
    [KernelFunction, Description("Get health factors for all open DeFi positions on Morpho and Balancer. Flags positions at liquidation risk.")]
    public async Task<string> GetHealthFactors(
        [Description("Protocol filter: 'morpho', 'balancer', or omit to return all protocols")] string? protocol = null,
        CancellationToken ct = default)
    {
        try
        {
            using var client = CreateDeFiClient();
            var path = string.IsNullOrWhiteSpace(protocol)
                ? "/api/positions/health"
                : $"/api/positions/health?protocol={Uri.EscapeDataString(protocol)}";

            var positions = await client.GetFromJsonAsync<List<HealthFactorDto>>(path, ct)
                                        .ConfigureAwait(false) ?? [];

            if (positions.Count == 0)
                return protocol is null ? "No open DeFi positions." : $"No open positions on {protocol}.";

            var lines = positions
                .OrderBy(p => p.HealthFactor)
                .Select(p =>
                {
                    var alert = p.HealthFactor < 1.0m ? " 🚨 LIQUIDATABLE" :
                                p.HealthFactor < 1.2m ? " ⚠️ AT RISK" : "";
                    return $"  {p.Protocol,-10} | HF: {p.HealthFactor:F3} | " +
                           $"Collateral: ${p.CollateralUsd:N0} | Borrow: ${p.BorrowUsd:N0} | {p.Severity}{alert}";
                });

            var risky = positions.Count(p => p.HealthFactor < 1.5m);
            return $"DeFi positions ({positions.Count}, {risky} at risk):\n" + string.Join("\n", lines);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeFiPlugin.GetHealthFactors failed");
            return "Unable to retrieve DeFi health factors — DeFi module may be unavailable.";
        }
    }

    /// <summary>
    /// Simulate a portfolio rebalance across DeFi protocols without executing any transactions.
    /// Returns estimated gas costs, slippage impact, and projected APY improvement.
    /// </summary>
    [KernelFunction, Description("Simulate a DeFi portfolio rebalance across Morpho/Balancer without executing. Returns estimated gas, slippage, and APY impact.")]
    public async Task<string> SimulateRebalance(
        [Description("Target allocation as JSON (e.g. '{\"morpho_supply\": 0.6, \"balancer_lp\": 0.4}')")] string targetAllocationJson,
        CancellationToken ct = default)
    {
        JsonElement targetAllocation;
        try
        {
            targetAllocation = JsonSerializer.Deserialize<JsonElement>(targetAllocationJson);
        }
        catch (JsonException)
        {
            return $"Invalid JSON for target allocation: {targetAllocationJson}";
        }

        try
        {
            using var client = CreateDeFiClient();
            var request = new { target_allocation = targetAllocation, simulate_only = true };
            var response = await client.PostAsJsonAsync("/api/rebalance/simulate", request, ct)
                                       .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var sim = await response.Content.ReadFromJsonAsync<RebalanceSimDto>(ct).ConfigureAwait(false);
            if (sim is null)
                return "Simulation completed but no result returned.";

            // Open simulation results on canvas
            var panelData = JsonSerializer.SerializeToElement(sim);
            await _canvasDispatcher.DispatchAsync(
                new OpenPanelAction("DeFiSimulation", panelData, "Rebalance Simulation"),
                UserId, ct).ConfigureAwait(false);

            return $"Rebalance simulation:\n" +
                   $"  Estimated gas:    {sim.EstimatedGasUsd:F2} USD\n" +
                   $"  Slippage impact:  {sim.SlippagePercent:F3}%\n" +
                   $"  Projected APY:    {sim.ProjectedApyPercent:F2}%\n" +
                   $"  Current APY:      {sim.CurrentApyPercent:F2}%\n" +
                   $"  Net benefit:      {sim.ProjectedApyPercent - sim.CurrentApyPercent:+0.00;-0.00}% APY\n" +
                   "Simulation results opened on canvas.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeFiPlugin.SimulateRebalance failed");
            return "Failed to simulate rebalance — DeFi module may be unavailable.";
        }
    }

    /// <summary>
    /// Get current APYs for Balancer liquidity pools and Morpho lending markets on Arbitrum.
    /// </summary>
    [KernelFunction, Description("Get current APYs for Balancer pools and Morpho lending markets on Arbitrum")]
    public async Task<string> GetPoolAPYs(
        [Description("Protocol to query: 'balancer', 'morpho', or omit for both")] string? protocol = null,
        [Description("Minimum APY filter in percent (e.g. 5 for only pools above 5% APY)")] decimal minApyPercent = 0,
        CancellationToken ct = default)
    {
        try
        {
            using var client = CreateDeFiClient();
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(protocol))
                queryParams.Add($"protocol={Uri.EscapeDataString(protocol)}");
            if (minApyPercent > 0)
                queryParams.Add($"min_apy={minApyPercent}");

            var path = "/api/pools/apy" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");

            var pools = await client.GetFromJsonAsync<List<PoolApyDto>>(path, ct).ConfigureAwait(false) ?? [];

            if (pools.Count == 0)
                return protocol is null ? "No pool APY data available." : $"No {protocol} pools found.";

            var sorted = pools.OrderByDescending(p => p.ApyPercent);
            var lines = sorted.Select(p =>
                $"  {p.Protocol,-10} | {p.PoolName,-30} | APY: {p.ApyPercent:F2}% | TVL: ${p.TvlUsd:N0}");

            return $"Pool APYs ({pools.Count} pools{(protocol is not null ? $" on {protocol}" : "")}):\n" +
                   string.Join("\n", lines);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeFiPlugin.GetPoolAPYs failed");
            return "Unable to retrieve pool APYs — DeFi module may be unavailable.";
        }
    }

    /// <summary>
    /// Get a summary of the current DeFi portfolio: total collateral, total borrow, weighted average APY,
    /// and aggregate health factor.
    /// </summary>
    [KernelFunction, Description("Get a DeFi portfolio summary: total collateral, borrow exposure, weighted APY, and aggregate health factor")]
    public async Task<string> GetPortfolioSummary(CancellationToken ct = default)
    {
        try
        {
            using var client = CreateDeFiClient();
            var summary = await client.GetFromJsonAsync<PortfolioSummaryDto>("/api/portfolio/summary", ct)
                                      .ConfigureAwait(false);
            if (summary is null)
                return "DeFi portfolio summary unavailable.";

            return $"DeFi Portfolio Summary:\n" +
                   $"  Total collateral: ${summary.TotalCollateralUsd:N0}\n" +
                   $"  Total borrowed:   ${summary.TotalBorrowUsd:N0}\n" +
                   $"  Utilisation:      {summary.UtilisationPercent:F1}%\n" +
                   $"  Weighted APY:     {summary.WeightedApyPercent:F2}%\n" +
                   $"  Aggregate HF:     {summary.AggregateHealthFactor:F3}\n" +
                   $"  Open positions:   {summary.PositionCount}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeFiPlugin.GetPortfolioSummary failed");
            return "Unable to retrieve DeFi portfolio summary.";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateDeFiClient()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.DeFiUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record HealthFactorDto(
        [property: JsonPropertyName("position_id")]   string PositionId,
        [property: JsonPropertyName("protocol")]      string Protocol,
        [property: JsonPropertyName("health_factor")] decimal HealthFactor,
        [property: JsonPropertyName("collateral_usd")] decimal CollateralUsd,
        [property: JsonPropertyName("borrow_usd")]    decimal BorrowUsd,
        [property: JsonPropertyName("severity")]      string Severity);

    private sealed record RebalanceSimDto(
        [property: JsonPropertyName("estimated_gas_usd")]     decimal EstimatedGasUsd,
        [property: JsonPropertyName("slippage_percent")]      decimal SlippagePercent,
        [property: JsonPropertyName("projected_apy_percent")] decimal ProjectedApyPercent,
        [property: JsonPropertyName("current_apy_percent")]   decimal CurrentApyPercent);

    private sealed record PoolApyDto(
        [property: JsonPropertyName("protocol")]   string Protocol,
        [property: JsonPropertyName("pool_name")]  string PoolName,
        [property: JsonPropertyName("apy_percent")] decimal ApyPercent,
        [property: JsonPropertyName("tvl_usd")]    decimal TvlUsd);

    private sealed record PortfolioSummaryDto(
        [property: JsonPropertyName("total_collateral_usd")]   decimal TotalCollateralUsd,
        [property: JsonPropertyName("total_borrow_usd")]       decimal TotalBorrowUsd,
        [property: JsonPropertyName("utilisation_percent")]    decimal UtilisationPercent,
        [property: JsonPropertyName("weighted_apy_percent")]   decimal WeightedApyPercent,
        [property: JsonPropertyName("aggregate_health_factor")] decimal AggregateHealthFactor,
        [property: JsonPropertyName("position_count")]         int PositionCount);
}
