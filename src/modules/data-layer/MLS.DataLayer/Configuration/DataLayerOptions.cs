namespace MLS.DataLayer.Configuration;

/// <summary>
/// Configuration options for the Data Layer module, bound from <c>appsettings.json</c>
/// under the <c>"DataLayer"</c> section.
/// </summary>
public sealed class DataLayerOptions
{
    /// <summary>HTTP endpoint of this module, e.g. <c>http://data-layer:5700</c>.</summary>
    public string HttpEndpoint { get; set; } = "http://data-layer:5700";

    /// <summary>WebSocket endpoint of this module, e.g. <c>ws://data-layer:6700</c>.</summary>
    public string WsEndpoint { get; set; } = "ws://data-layer:6700";

    /// <summary>Block Controller HTTP base URL, e.g. <c>http://block-controller:5100</c>.</summary>
    public string BlockControllerUrl { get; set; } = "http://block-controller:5100";

    /// <summary>PostgreSQL connection string for candle and feature storage.</summary>
    public string PostgresConnectionString { get; set; } =
        "Host=postgres;Port=5432;Database=mls_db;Username=mls_user;Password=mls";

    /// <summary>HYPERLIQUID REST base URL.</summary>
    public string HyperliquidRestUrl { get; set; } = "https://api.hyperliquid.xyz";

    /// <summary>HYPERLIQUID WebSocket URL.</summary>
    public string HyperliquidWsUrl { get; set; } = "wss://api.hyperliquid.xyz/ws";

    /// <summary>Arbitrum One JSON-RPC endpoint used for on-chain candle reads (Camelot).</summary>
    public string ArbitrumRpcUrl { get; set; } = "https://arb1.arbitrum.io/rpc";

    /// <summary>
    /// Gap detection timer interval in seconds.  Defaults to 60 (run every minute).
    /// </summary>
    public int GapDetectorIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of concurrent backfill jobs in the pipeline queue.
    /// </summary>
    public int BackfillQueueCapacity { get; set; } = 64;

    /// <summary>Maximum candles per REST backfill chunk request.</summary>
    public int BackfillChunkSize { get; set; } = 1000;
}
