namespace MLS.Network.Runtime.Constants;

/// <summary>Network constants for the runtime module.</summary>
public static class RuntimeConstants
{
    /// <summary>HTTP API port.</summary>
    public const int HttpPort = 5013;

    /// <summary>WebSocket port.</summary>
    public const int WsPort = 6013;

    /// <summary>Canonical module name used for Block Controller registration.</summary>
    public const string ModuleName = "runtime";

    /// <summary>Docker container name.</summary>
    public const string ContainerName = "mls-runtime";

    /// <summary>Docker image reference.</summary>
    public const string DockerImage = "ghcr.io/somat3k/mls-runtime:latest";

    /// <summary>Docker label used to identify MLS-managed containers.</summary>
    public const string MlsModuleLabel = "com.mls.module";
}
