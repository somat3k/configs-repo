using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLS.AIHub.Context;

/// <summary>
/// Typed snapshot of the live MLS platform state assembled by <see cref="ContextAssembler"/>.
/// Used as system context for Semantic Kernel LLM invocations.
/// </summary>
public sealed record ProjectSnapshot
{
    /// <summary>UTC timestamp when the snapshot was assembled.</summary>
    [JsonPropertyName("assembled_at")]
    public DateTimeOffset AssembledAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Total milliseconds spent assembling the snapshot.</summary>
    [JsonPropertyName("assembly_ms")]
    public long AssemblyMs { get; init; }

    /// <summary>List of all registered modules with their current health status.</summary>
    [JsonPropertyName("modules")]
    public IReadOnlyList<ModuleInfo> Modules { get; init; } = [];

    /// <summary>Open trading positions with current P&amp;L.</summary>
    [JsonPropertyName("open_positions")]
    public IReadOnlyList<PositionInfo> OpenPositions { get; init; } = [];

    /// <summary>Recent ML trading signals.</summary>
    [JsonPropertyName("recent_signals")]
    public IReadOnlyList<SignalInfo> RecentSignals { get; init; } = [];

    /// <summary>Active arbitrage opportunities.</summary>
    [JsonPropertyName("arb_opportunities")]
    public IReadOnlyList<JsonElement> ArbOpportunities { get; init; } = [];

    /// <summary>DeFi position health factors.</summary>
    [JsonPropertyName("defi_health")]
    public IReadOnlyList<DefiHealthInfo> DefiHealth { get; init; } = [];

    /// <summary>Registered ML models with their latest metrics.</summary>
    [JsonPropertyName("ml_models")]
    public IReadOnlyList<ModelInfo> MlModels { get; init; } = [];

    /// <summary>Active strategy graph summaries from Designer.</summary>
    [JsonPropertyName("active_strategies")]
    public IReadOnlyList<StrategyInfo> ActiveStrategies { get; init; } = [];

    /// <summary>Recent platform envelope history (last N envelopes).</summary>
    [JsonPropertyName("envelope_history")]
    public IReadOnlyList<JsonElement> EnvelopeHistory { get; init; } = [];

    /// <summary>Sources that failed or timed out during assembly (for diagnostics).</summary>
    [JsonPropertyName("failed_sources")]
    public IReadOnlyList<string> FailedSources { get; init; } = [];
}

/// <summary>Registered module identity and health.</summary>
public sealed record ModuleInfo
{
    /// <summary>Module identifier (e.g. <c>trader</c>, <c>ml-runtime</c>).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>HTTP endpoint base URL.</summary>
    [JsonPropertyName("endpoint_http")]
    public string EndpointHttp { get; init; } = string.Empty;

    /// <summary>Module health status.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "unknown";

    /// <summary>Last heartbeat UTC timestamp.</summary>
    [JsonPropertyName("last_heartbeat")]
    public DateTimeOffset? LastHeartbeat { get; init; }
}

/// <summary>An open trading position with current P&amp;L.</summary>
public sealed record PositionInfo
{
    /// <summary>Trading symbol (e.g. <c>BTC-PERP</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Position side: <c>LONG</c> or <c>SHORT</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    /// <summary>Position size in base currency.</summary>
    [JsonPropertyName("size")]
    public decimal Size { get; init; }

    /// <summary>Average entry price.</summary>
    [JsonPropertyName("entry_price")]
    public decimal EntryPrice { get; init; }

    /// <summary>Current mark price.</summary>
    [JsonPropertyName("mark_price")]
    public decimal MarkPrice { get; init; }

    /// <summary>Unrealised P&amp;L in USD.</summary>
    [JsonPropertyName("unrealised_pnl")]
    public decimal UnrealisedPnl { get; init; }

    /// <summary>Leverage multiplier.</summary>
    [JsonPropertyName("leverage")]
    public decimal Leverage { get; init; }
}

/// <summary>An ML-generated trading signal.</summary>
public sealed record SignalInfo
{
    /// <summary>Trading symbol the signal applies to.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Signal direction: <c>BUY</c>, <c>SELL</c>, or <c>HOLD</c>.</summary>
    [JsonPropertyName("direction")]
    public string Direction { get; init; } = string.Empty;

    /// <summary>Model confidence score in [0, 1].</summary>
    [JsonPropertyName("confidence")]
    public float Confidence { get; init; }

    /// <summary>Model type that generated the signal.</summary>
    [JsonPropertyName("model_type")]
    public string ModelType { get; init; } = string.Empty;

    /// <summary>UTC timestamp of signal generation.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>DeFi position health factor summary.</summary>
public sealed record DefiHealthInfo
{
    /// <summary>Position or protocol identifier.</summary>
    [JsonPropertyName("position_id")]
    public string PositionId { get; init; } = string.Empty;

    /// <summary>Protocol name (e.g. <c>Morpho</c>, <c>Balancer</c>).</summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = string.Empty;

    /// <summary>Aave-style health factor (&gt;1 = safe, &lt;1 = liquidatable).</summary>
    [JsonPropertyName("health_factor")]
    public decimal HealthFactor { get; init; }

    /// <summary>Collateral value in USD.</summary>
    [JsonPropertyName("collateral_usd")]
    public decimal CollateralUsd { get; init; }

    /// <summary>Borrow value in USD.</summary>
    [JsonPropertyName("borrow_usd")]
    public decimal BorrowUsd { get; init; }

    /// <summary>
    /// Health severity: <c>Healthy</c>, <c>Warning</c>, <c>Critical</c>, or <c>Liquidatable</c>.
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "Healthy";
}

/// <summary>Registered ML model with latest training metrics.</summary>
public sealed record ModelInfo
{
    /// <summary>Model identifier key (e.g. <c>model-t</c>, <c>model-a</c>).</summary>
    [JsonPropertyName("model_id")]
    public string ModelId { get; init; } = string.Empty;

    /// <summary>Model type: <c>Trading</c>, <c>Arbitrage</c>, or <c>DeFi</c>.</summary>
    [JsonPropertyName("model_type")]
    public string ModelType { get; init; } = string.Empty;

    /// <summary>Training accuracy (0–1).</summary>
    [JsonPropertyName("accuracy")]
    public float Accuracy { get; init; }

    /// <summary>Current deployment state.</summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>UTC timestamp of last training run.</summary>
    [JsonPropertyName("last_trained")]
    public DateTimeOffset? LastTrained { get; init; }
}

/// <summary>Active strategy graph summary from Designer.</summary>
public sealed record StrategyInfo
{
    /// <summary>Strategy unique identifier.</summary>
    [JsonPropertyName("strategy_id")]
    public Guid StrategyId { get; init; }

    /// <summary>Strategy display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Current state: <c>Running</c>, <c>Stopped</c>, <c>Backtesting</c>.</summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>Number of blocks in the strategy graph.</summary>
    [JsonPropertyName("block_count")]
    public int BlockCount { get; init; }

    /// <summary>UTC creation timestamp.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}
