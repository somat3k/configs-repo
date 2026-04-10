namespace MLS.Network.VirtualMachine.Models;

/// <summary>Configuration for the virtual-machine module bound from appsettings.json.</summary>
public sealed class VirtualMachineConfig
{
    /// <summary>HTTP endpoint reported during Block Controller registration.</summary>
    public string HttpEndpoint { get; init; } = "http://virtual-machine:5014";

    /// <summary>WebSocket endpoint reported during Block Controller registration.</summary>
    public string WsEndpoint { get; init; } = "ws://virtual-machine:6014";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>Redis connection string (optional).</summary>
    public string RedisConnectionString { get; init; } = "redis:6379";
}
