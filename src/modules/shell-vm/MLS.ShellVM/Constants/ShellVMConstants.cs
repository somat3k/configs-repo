namespace MLS.ShellVM.Constants;

/// <summary>All shell-vm message type constants for the Envelope Protocol.</summary>
public static class ShellVMMessageTypes
{
    /// <summary>Request to execute a command inside an existing shell session.</summary>
    public const string ExecRequest = "SHELL_EXEC_REQUEST";

    /// <summary>Raw stdin bytes sent to an interactive PTY session.</summary>
    public const string Input = "SHELL_INPUT";

    /// <summary>PTY resize request.</summary>
    public const string Resize = "SHELL_RESIZE";

    /// <summary>stdout / stderr output chunk from a shell session.</summary>
    public const string Output = "SHELL_OUTPUT";

    /// <summary>Shell session state transition notification.</summary>
    public const string SessionState = "SHELL_SESSION_STATE";

    /// <summary>Broadcast when a new session is created.</summary>
    public const string SessionCreated = "SHELL_SESSION_CREATED";

    /// <summary>Broadcast when a session terminates.</summary>
    public const string SessionTerminated = "SHELL_SESSION_TERMINATED";
}

/// <summary>Network and port constants for the shell-vm module.</summary>
public static class ShellVMNetworkConstants
{
    /// <summary>HTTP API port.</summary>
    public const int HttpPort = 5950;

    /// <summary>WebSocket port.</summary>
    public const int WsPort = 6950;

    /// <summary>Canonical module name used for Block Controller registration.</summary>
    public const string ModuleName = "shell-vm";

    /// <summary>Docker container name.</summary>
    public const string ContainerName = "mls-shell-vm";

    /// <summary>Docker image reference.</summary>
    public const string DockerImage = "ghcr.io/somat3k/mls-shell-vm:latest";
}

/// <summary>Capacity and timing limits for the shell-vm module.</summary>
public static class ShellVMLimits
{
    /// <summary>Bounded capacity of each session's output <see cref="System.Threading.Channels.Channel{T}"/>.</summary>
    public const int OutputChannelCapacity = 4096;

    /// <summary>Maximum number of output lines kept in the Redis ring-buffer per session.</summary>
    public const int RedisRingBufferLines = 10_000;

    /// <summary>Prefix for Redis keys storing session state.</summary>
    public const string RedisSessionPrefix = "shellvm:session:";

    /// <summary>Prefix for Redis keys storing output ring-buffer lists.</summary>
    public const string RedisOutputPrefix = "shellvm:output:";

    /// <summary>TTL for session state stored in Redis (24 hours).</summary>
    public static readonly TimeSpan SessionRedisTtl = TimeSpan.FromHours(24);

    /// <summary>Interval at which <c>SessionWatchdog</c> scans for idle sessions.</summary>
    public static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(30);

    /// <summary>Grace period between SIGTERM and SIGKILL when cancelling a command.</summary>
    public static readonly TimeSpan SigtermGracePeriod = TimeSpan.FromSeconds(5);
}
