namespace MLS.Network.UniqueIdGenerator.Models;

/// <summary>Configuration for the unique-id-generator module bound from appsettings.json.</summary>
public sealed class UniqueIdGeneratorConfig
{
    /// <summary>HTTP endpoint reported during Block Controller registration.</summary>
    public string HttpEndpoint { get; init; } = "http://unique-id-generator:5010";

    /// <summary>WebSocket endpoint reported during Block Controller registration.</summary>
    public string WsEndpoint { get; init; } = "ws://unique-id-generator:6010";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>Redis connection string (optional).</summary>
    public string RedisConnectionString { get; init; } = "redis:6379";
}
