namespace MLS.Transactions.Configuration;

/// <summary>Typed configuration for the Transactions module.</summary>
public sealed class TransactionsOptions
{
    /// <summary>HTTP endpoint exposed by this module.</summary>
    public string HttpEndpoint { get; init; } = "http://transactions:5900";

    /// <summary>WebSocket endpoint exposed by this module.</summary>
    public string WsEndpoint { get; init; } = "ws://transactions:6900";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>PostgreSQL connection string.</summary>
    public string PostgresConnectionString { get; init; } = "";

    /// <summary>Redis connection string.</summary>
    public string RedisConnectionString { get; init; } = "";
}
