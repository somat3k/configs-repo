namespace MLS.Network.Runtime.Models;

/// <summary>Configuration for the runtime module bound from appsettings.json.</summary>
public sealed class RuntimeConfig
{
    /// <summary>HTTP endpoint reported during Block Controller registration.</summary>
    public string HttpEndpoint { get; init; } = "http://runtime:5013";

    /// <summary>WebSocket endpoint reported during Block Controller registration.</summary>
    public string WsEndpoint { get; init; } = "ws://runtime:6013";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>Redis connection string (optional).</summary>
    public string RedisConnectionString { get; init; } = "redis:6379";

    /// <summary>Docker socket path for the Docker client connection.</summary>
    public string DockerSocketPath { get; init; } = "unix:///var/run/docker.sock";
}
