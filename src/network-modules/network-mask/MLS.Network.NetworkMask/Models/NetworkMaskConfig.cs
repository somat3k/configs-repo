using MLS.Network.NetworkMask.Services;

namespace MLS.Network.NetworkMask.Models;

/// <summary>Configuration for the network-mask module bound from appsettings.json.</summary>
public sealed class NetworkMaskConfig
{
    /// <summary>HTTP endpoint reported during Block Controller registration.</summary>
    public string HttpEndpoint { get; init; } = "http://network-mask:5016";

    /// <summary>WebSocket endpoint reported during Block Controller registration.</summary>
    public string WsEndpoint { get; init; } = "ws://network-mask:6016";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>Redis connection string (optional).</summary>
    public string RedisConnectionString { get; init; } = "redis:6379";

    /// <summary>Pre-seeded known endpoints loaded from appsettings.json.</summary>
    public EndpointRegistration[] KnownEndpoints { get; init; } = [];
}
