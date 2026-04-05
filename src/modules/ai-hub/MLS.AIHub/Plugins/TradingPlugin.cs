using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Plugins;

/// <summary>
/// Semantic Kernel plugin that gives the AI direct access to live trading data,
/// positions, ML signals, and order placement via the Trader module.
/// </summary>
public sealed class TradingPlugin(
    IHttpClientFactory _httpFactory,
    IOptions<AIHubOptions> _options,
    ILogger<TradingPlugin> _logger)
{
    /// <summary>
    /// Get all open trading positions from the Trader module, optionally filtered by symbol.
    /// Returns position details including side, size, entry price, mark price, and unrealised P&amp;L.
    /// </summary>
    [KernelFunction, Description("Get all open trading positions with current P&L, optionally filtered by symbol")]
    public async Task<string> GetPositions(
        [Description("Symbol filter (e.g. 'BTC-PERP'). Leave empty to return all open positions.")] string? symbol = null,
        CancellationToken ct = default)
    {
        var path = string.IsNullOrWhiteSpace(symbol)
            ? "/api/positions"
            : $"/api/positions?symbol={Uri.EscapeDataString(symbol)}";

        try
        {
            using var client = CreateTraderClient();
            var positions = await client.GetFromJsonAsync<List<PositionDto>>(path, ct)
                                        .ConfigureAwait(false) ?? [];

            if (positions.Count == 0)
                return symbol is null ? "No open positions." : $"No open positions for {symbol}.";

            var lines = positions.Select(p =>
                $"{p.Symbol} {p.Side} | Size: {p.Size} | Entry: {p.EntryPrice:F4} | Mark: {p.MarkPrice:F4} | uPnL: {p.UnrealisedPnl:+0.00;-0.00} USD | {p.Leverage}x");

            return $"Open positions ({positions.Count}):\n" + string.Join("\n", lines);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TradingPlugin.GetPositions failed");
            return "Unable to retrieve positions — Trader module may be unavailable.";
        }
    }

    /// <summary>
    /// Place a trading order on the configured exchange (Hyperliquid).
    /// The user MUST explicitly confirm before execution by setting <paramref name="confirmed"/> to true.
    /// </summary>
    [KernelFunction, Description("Place a trading order on the configured exchange (Hyperliquid). Requires explicit user confirmation.")]
    public async Task<string> PlaceOrder(
        [Description("Trading symbol (e.g. 'BTC-PERP')")] string symbol,
        [Description("Order side: 'BUY' or 'SELL'")] string side,
        [Description("Order quantity in base currency (e.g. 0.01 for 0.01 BTC)")] decimal quantity,
        [Description("Limit price in USD. Omit for market orders.")] decimal? limitPrice = null,
        [Description("Set to true only after the user has explicitly confirmed the order details.")] bool confirmed = false,
        CancellationToken ct = default)
    {
        if (!confirmed)
        {
            var orderType = limitPrice.HasValue ? $"limit @ {limitPrice:F4}" : "market";
            return $"Please confirm: {side} {quantity} {symbol} ({orderType}). Reply 'confirm order' to execute.";
        }

        try
        {
            using var client = CreateTraderClient();
            var request = new { symbol, side = side.ToUpperInvariant(), quantity, limit_price = limitPrice };
            var response = await client.PostAsJsonAsync("/api/orders", request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OrderResultDto>(ct).ConfigureAwait(false);
            return result is null
                ? "Order submitted but no response body returned."
                : $"Order placed: {result.OrderId} | Symbol: {result.Symbol} | Status: {result.Status} | Filled: {result.FilledQuantity}/{quantity}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TradingPlugin.PlaceOrder failed");
            return "Order placement failed — Trader module may be unavailable or the order was rejected.";
        }
    }

    /// <summary>
    /// Get the most recent ML signal history for a symbol, showing direction, confidence, and timestamp.
    /// </summary>
    [KernelFunction, Description("Get recent ML trading signal history for a symbol, showing direction (BUY/SELL/HOLD) and confidence scores")]
    public async Task<string> GetSignalHistory(
        [Description("Trading symbol to retrieve signals for (e.g. 'BTC-PERP')")] string symbol,
        [Description("Number of signals to return (default 20, max 100)")] int count = 20,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 100);
        try
        {
            using var client = CreateTraderClient();
            var signals = await client
                .GetFromJsonAsync<List<SignalDto>>(
                    $"/api/signals/recent?symbol={Uri.EscapeDataString(symbol)}&n={count}", ct)
                .ConfigureAwait(false) ?? [];

            if (signals.Count == 0)
                return $"No recent signals found for {symbol}.";

            var lines = signals.Select(s =>
                $"{s.Timestamp:yyyy-MM-dd HH:mm} {s.Direction,5} | Conf: {s.Confidence * 100:F0}% | Model: {s.ModelType}");

            return $"Last {signals.Count} signals for {symbol}:\n" + string.Join("\n", lines);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TradingPlugin.GetSignalHistory failed");
            return $"Unable to retrieve signal history for {symbol}.";
        }
    }

    /// <summary>
    /// Get the total realised and unrealised P&amp;L summary across all open positions and closed trades.
    /// </summary>
    [KernelFunction, Description("Get a P&L summary showing total unrealised P&L, today's realised P&L, and win rate")]
    public async Task<string> GetPnLSummary(CancellationToken ct = default)
    {
        try
        {
            using var client = CreateTraderClient();
            var summary = await client.GetFromJsonAsync<PnLSummaryDto>("/api/pnl/summary", ct)
                                      .ConfigureAwait(false);
            if (summary is null)
                return "P&L summary unavailable.";

            return $"P&L Summary:\n" +
                   $"  Unrealised: {summary.TotalUnrealisedPnl:+0.00;-0.00} USD\n" +
                   $"  Realised today: {summary.RealisedToday:+0.00;-0.00} USD\n" +
                   $"  Win rate (30d): {summary.WinRate30d * 100:F0}%\n" +
                   $"  Open positions: {summary.OpenPositionCount}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TradingPlugin.GetPnLSummary failed");
            return "Unable to retrieve P&L summary — Trader module may be unavailable.";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateTraderClient()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.TraderUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
        return client;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record PositionDto(
        [property: JsonPropertyName("symbol")]        string Symbol,
        [property: JsonPropertyName("side")]          string Side,
        [property: JsonPropertyName("size")]          decimal Size,
        [property: JsonPropertyName("entry_price")]   decimal EntryPrice,
        [property: JsonPropertyName("mark_price")]    decimal MarkPrice,
        [property: JsonPropertyName("unrealised_pnl")] decimal UnrealisedPnl,
        [property: JsonPropertyName("leverage")]      decimal Leverage);

    private sealed record OrderResultDto(
        [property: JsonPropertyName("order_id")]       string OrderId,
        [property: JsonPropertyName("symbol")]         string Symbol,
        [property: JsonPropertyName("status")]         string Status,
        [property: JsonPropertyName("filled_quantity")] decimal FilledQuantity);

    private sealed record SignalDto(
        [property: JsonPropertyName("symbol")]     string Symbol,
        [property: JsonPropertyName("direction")]  string Direction,
        [property: JsonPropertyName("confidence")] float Confidence,
        [property: JsonPropertyName("model_type")] string ModelType,
        [property: JsonPropertyName("timestamp")]  DateTimeOffset Timestamp);

    private sealed record PnLSummaryDto(
        [property: JsonPropertyName("total_unrealised_pnl")] decimal TotalUnrealisedPnl,
        [property: JsonPropertyName("realised_today")]       decimal RealisedToday,
        [property: JsonPropertyName("win_rate_30d")]         float WinRate30d,
        [property: JsonPropertyName("open_position_count")]  int OpenPositionCount);
}
