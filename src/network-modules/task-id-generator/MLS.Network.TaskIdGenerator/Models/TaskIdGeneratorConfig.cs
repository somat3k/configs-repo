namespace MLS.Network.TaskIdGenerator.Models;

/// <summary>Configuration for the task-id-generator module bound from appsettings.json.</summary>
public sealed class TaskIdGeneratorConfig
{
    /// <summary>HTTP endpoint reported during Block Controller registration.</summary>
    public string HttpEndpoint { get; init; } = "http://task-id-generator:5011";

    /// <summary>WebSocket endpoint reported during Block Controller registration.</summary>
    public string WsEndpoint { get; init; } = "ws://task-id-generator:6011";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>Redis connection string (optional).</summary>
    public string RedisConnectionString { get; init; } = "redis:6379";
}
