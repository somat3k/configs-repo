namespace MLS.DeFi.Configuration;

/// <summary>
/// Configuration options for the DeFi module, bound from <c>appsettings.json</c>
/// under the <c>"DeFi"</c> section.
/// </summary>
public sealed class DeFiOptions
{
    /// <summary>HTTP endpoint of this module, e.g. <c>http://defi:5500</c>.</summary>
    public string HttpEndpoint { get; set; } = "http://defi:5500";

    /// <summary>WebSocket endpoint of this module, e.g. <c>ws://defi:6500</c>.</summary>
    public string WsEndpoint { get; set; } = "ws://defi:6500";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; set; } = "http://block-controller:5100";

    /// <summary>PostgreSQL connection string for DeFi transaction and position storage.</summary>
    public string PostgresConnectionString { get; set; } =
        "Host=postgres;Port=5432;Database=mls_db;Username=mls_user;Password=mls";

    /// <summary>Redis connection string for hot caches.</summary>
    public string RedisConnectionString { get; set; } = "redis:6379";

    /// <summary>HYPERLIQUID REST API base URL.</summary>
    public string HyperliquidRestUrl { get; set; } = "https://api.hyperliquid.xyz";

    /// <summary>HYPERLIQUID WebSocket URL.</summary>
    public string HyperliquidWsUrl { get; set; } = "wss://api.hyperliquid.xyz/ws";

    /// <summary>
    /// Ordered list of venue IDs for the fallback chain.
    /// The first entry is always HYPERLIQUID (primary); subsequent entries are
    /// alternative venues tried in order when HYPERLIQUID is unavailable.
    /// These are plain configuration identifiers — they are not looked up in
    /// the <c>blockchain_addresses</c> table (only contract addresses are stored there).
    /// </summary>
    public string[] FallbackChain { get; set; } = ["hyperliquid"];

    /// <summary>Timeout in seconds for a single order placement attempt per venue.</summary>
    public int OrderTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of position notification messages queued before the producer blocks.
    /// </summary>
    public int PositionChannelCapacity { get; set; } = 512;

    /// <summary>
    /// Interval in seconds between position polling cycles for the background monitor.
    /// </summary>
    public int PositionPollIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Wallet provider backend type. Supported values: <c>env</c> (environment variable),
    /// <c>vault</c> (HashiCorp Vault), <c>hsm</c> (hardware security module).
    /// </summary>
    public string WalletBackend { get; set; } = "env";

    /// <summary>
    /// RPC endpoint for on-chain transaction broadcasting (EVM-compatible).
    /// </summary>
    public string ChainRpcUrl { get; set; } = "https://arb1.arbitrum.io/rpc";

    /// <summary>Chain ID for the target network. 42161 = Arbitrum One.</summary>
    public int ChainId { get; set; } = 42161;

    /// <summary>Gas price multiplier applied to on-chain transactions (e.g. 1.15 = +15%).</summary>
    public decimal GasPriceMultiplier { get; set; } = 1.15m;
}
