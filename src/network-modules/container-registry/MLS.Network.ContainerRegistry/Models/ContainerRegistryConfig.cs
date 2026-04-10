namespace MLS.Network.ContainerRegistry.Models;

/// <summary>Configuration for the container-registry module bound from appsettings.json.</summary>
public sealed class ContainerRegistryConfig
{
    /// <summary>HTTP endpoint reported during Block Controller registration.</summary>
    public string HttpEndpoint { get; init; } = "http://container-registry:5015";

    /// <summary>WebSocket endpoint reported during Block Controller registration.</summary>
    public string WsEndpoint { get; init; } = "ws://container-registry:6015";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>Redis connection string (optional).</summary>
    public string RedisConnectionString { get; init; } = "redis:6379";
}
