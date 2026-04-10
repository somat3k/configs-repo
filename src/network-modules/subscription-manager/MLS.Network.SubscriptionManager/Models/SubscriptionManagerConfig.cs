namespace MLS.Network.SubscriptionManager.Models;

/// <summary>Configuration for the subscription-manager module bound from appsettings.json.</summary>
public sealed class SubscriptionManagerConfig
{
    /// <summary>HTTP endpoint reported during Block Controller registration.</summary>
    public string HttpEndpoint { get; init; } = "http://subscription-manager:5012";

    /// <summary>WebSocket endpoint reported during Block Controller registration.</summary>
    public string WsEndpoint { get; init; } = "ws://subscription-manager:6012";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>Redis connection string (optional — enables cross-instance pub/sub).</summary>
    public string RedisConnectionString { get; init; } = "redis:6379";
}
