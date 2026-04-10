namespace MLS.Network.TaskIdGenerator.Constants;

/// <summary>Network constants for the task-id-generator module.</summary>
public static class TaskIdGeneratorConstants
{
    /// <summary>HTTP API port.</summary>
    public const int HttpPort = 5011;

    /// <summary>WebSocket port.</summary>
    public const int WsPort = 6011;

    /// <summary>Canonical module name used for Block Controller registration.</summary>
    public const string ModuleName = "task-id-generator";

    /// <summary>Docker container name.</summary>
    public const string ContainerName = "mls-task-id-generator";

    /// <summary>Docker image reference.</summary>
    public const string DockerImage = "ghcr.io/somat3k/mls-task-id-generator:latest";
}
