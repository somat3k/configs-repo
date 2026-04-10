namespace MLS.Network.NetworkMask.Constants;

/// <summary>Network constants for the network-mask module.</summary>
public static class NetworkMaskConstants
{
    /// <summary>HTTP API port.</summary>
    public const int HttpPort = 5016;

    /// <summary>WebSocket port.</summary>
    public const int WsPort = 6016;

    /// <summary>Canonical module name used for Block Controller registration.</summary>
    public const string ModuleName = "network-mask";

    /// <summary>Docker container name.</summary>
    public const string ContainerName = "mls-network-mask";

    /// <summary>Docker image reference.</summary>
    public const string DockerImage = "ghcr.io/somat3k/mls-network-mask:latest";
}
