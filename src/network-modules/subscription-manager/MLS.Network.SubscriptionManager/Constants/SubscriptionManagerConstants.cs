namespace MLS.Network.SubscriptionManager.Constants;

/// <summary>Network constants for the subscription-manager module.</summary>
public static class SubscriptionManagerConstants
{
    /// <summary>HTTP API port.</summary>
    public const int HttpPort = 5012;

    /// <summary>WebSocket port.</summary>
    public const int WsPort = 6012;

    /// <summary>Canonical module name used for Block Controller registration.</summary>
    public const string ModuleName = "subscription-manager";

    /// <summary>Docker container name.</summary>
    public const string ContainerName = "mls-subscription-manager";

    /// <summary>Docker image reference.</summary>
    public const string DockerImage = "ghcr.io/somat3k/mls-subscription-manager:latest";
}
