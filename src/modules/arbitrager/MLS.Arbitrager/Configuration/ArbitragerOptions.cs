namespace MLS.Arbitrager.Configuration;

/// <summary>
/// Configuration options for the Arbitrager module, bound from <c>appsettings.json</c>
/// under the <c>"Arbitrager"</c> section.
/// </summary>
public sealed class ArbitragerOptions
{
    /// <summary>HTTP endpoint of this module, e.g. <c>http://arbitrager:5400</c>.</summary>
    public string HttpEndpoint { get; set; } = "http://arbitrager:5400";

    /// <summary>WebSocket endpoint of this module, e.g. <c>ws://arbitrager:6400</c>.</summary>
    public string WsEndpoint { get; set; } = "ws://arbitrager:6400";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; set; } = "http://block-controller:5100";

    /// <summary>PostgreSQL connection string (used by address book).</summary>
    public string PostgresConnectionString { get; set; } =
        "Host=postgres;Port=5432;Database=mls_db;Username=mls_user;Password=mls";

    /// <summary>Arbitrum One JSON-RPC endpoint for on-chain price reads.</summary>
    public string ArbitrumRpcUrl { get; set; } = "https://arb1.arbitrum.io/rpc";

    /// <summary>Hyperliquid REST base URL.</summary>
    public string HyperliquidRestUrl { get; set; } = "https://api.hyperliquid.xyz";

    /// <summary>Price scan interval in milliseconds (default 500 ms).</summary>
    public int ScanIntervalMs { get; set; } = 500;

    /// <summary>Minimum net profit in USD to emit an opportunity.</summary>
    public decimal MinProfitUsd { get; set; } = 0.5m;

    /// <summary>Minimum ONNX scorer confidence to forward an opportunity for execution.</summary>
    public float MinScorerConfidence { get; set; } = 0.6f;

    /// <summary>
    /// Path to the model-a ONNX file used by <see cref="MLS.Arbitrager.Scoring.OpportunityScorer"/>.
    /// When empty the scorer falls back to rule-based scoring.
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>Maximum number of queued opportunities before oldest is dropped.</summary>
    public int OpportunityQueueCapacity { get; set; } = 256;

    /// <summary>Notional input amount per simulated arbitrage path in USD.</summary>
    public decimal SimulatedInputAmountUsd { get; set; } = 1_000m;
}
