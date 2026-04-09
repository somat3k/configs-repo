namespace MLS.Broker.Configuration;

/// <summary>
/// Configuration options for the Broker module, bound from <c>appsettings.json</c>
/// under the <c>"Broker"</c> section.
/// </summary>
public sealed class BrokerOptions
{
    /// <summary>HTTP endpoint of this module, e.g. <c>http://broker:5800</c>.</summary>
    public string HttpEndpoint { get; set; } = "http://broker:5800";

    /// <summary>WebSocket endpoint of this module, e.g. <c>ws://broker:6800</c>.</summary>
    public string WsEndpoint { get; set; } = "ws://broker:6800";

    /// <summary>Block Controller HTTP base URL, e.g. <c>http://block-controller:5100</c>.</summary>
    public string BlockControllerUrl { get; set; } = "http://block-controller:5100";

    /// <summary>PostgreSQL connection string for order and position storage.</summary>
    public string PostgresConnectionString { get; set; } =
        "Host=postgres;Port=5432;Database=mls_db;Username=mls_user;Password=mls";

    /// <summary>Redis connection string for order ID cache and idempotency checks.</summary>
    public string RedisConnectionString { get; set; } = "redis:6379";

    /// <summary>HYPERLIQUID REST API base URL.</summary>
    public string HyperliquidRestUrl { get; set; } = "https://api.hyperliquid.xyz";

    /// <summary>HYPERLIQUID WebSocket URL.</summary>
    public string HyperliquidWsUrl { get; set; } = "wss://api.hyperliquid.xyz/ws";

    /// <summary>
    /// Ordered list of broker venue IDs for the fallback chain.
    /// The first entry is always HYPERLIQUID; subsequent entries are fallback venues.
    /// Values are resolved from the <c>blockchain_addresses</c> table at runtime.
    /// </summary>
    public string[] FallbackChain { get; set; } = ["hyperliquid"];

    /// <summary>
    /// Timeout in seconds for a single order placement attempt per venue.
    /// Defaults to 10 seconds.
    /// </summary>
    public int OrderTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of fill notification messages that can be queued before the
    /// producer blocks. Defaults to 1024.
    /// </summary>
    public int FillChannelCapacity { get; set; } = 1024;
}
