namespace MLS.Network.VirtualMachine.Constants;

/// <summary>Network constants for the virtual-machine module.</summary>
public static class VirtualMachineConstants
{
    /// <summary>HTTP API port.</summary>
    public const int HttpPort = 5014;

    /// <summary>WebSocket port.</summary>
    public const int WsPort = 6014;

    /// <summary>Canonical module name used for Block Controller registration.</summary>
    public const string ModuleName = "virtual-machine";

    /// <summary>Docker container name.</summary>
    public const string ContainerName = "mls-virtual-machine";

    /// <summary>Docker image reference.</summary>
    public const string DockerImage = "ghcr.io/somat3k/mls-virtual-machine:latest";
}
