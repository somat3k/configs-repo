namespace MLS.Network.ContainerRegistry.Constants;

/// <summary>Network constants for the container-registry module.</summary>
public static class ContainerRegistryConstants
{
    /// <summary>HTTP API port.</summary>
    public const int HttpPort = 5015;

    /// <summary>WebSocket port.</summary>
    public const int WsPort = 6015;

    /// <summary>Canonical module name used for Block Controller registration.</summary>
    public const string ModuleName = "container-registry";

    /// <summary>Docker container name.</summary>
    public const string ContainerName = "mls-container-registry";

    /// <summary>Docker image reference.</summary>
    public const string DockerImage = "ghcr.io/somat3k/mls-container-registry:latest";
}
