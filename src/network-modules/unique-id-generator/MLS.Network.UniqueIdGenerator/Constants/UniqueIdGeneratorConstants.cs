namespace MLS.Network.UniqueIdGenerator.Constants;

/// <summary>Network constants for the unique-id-generator module.</summary>
public static class UniqueIdGeneratorConstants
{
    /// <summary>HTTP API port.</summary>
    public const int HttpPort = 5010;

    /// <summary>WebSocket port.</summary>
    public const int WsPort = 6010;

    /// <summary>Canonical module name used for Block Controller registration.</summary>
    public const string ModuleName = "unique-id-generator";

    /// <summary>Docker container name.</summary>
    public const string ContainerName = "mls-unique-id-generator";

    /// <summary>Docker image reference.</summary>
    public const string DockerImage = "ghcr.io/somat3k/mls-unique-id-generator:latest";
}
