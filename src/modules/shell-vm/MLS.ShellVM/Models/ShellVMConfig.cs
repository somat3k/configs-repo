namespace MLS.ShellVM.Models;

/// <summary>
/// Strongly-typed configuration bound from <c>appsettings.json</c> under the <c>"ShellVM"</c> section.
/// </summary>
public sealed class ShellVMConfig
{
    /// <summary>Maximum number of concurrent shell sessions the module will manage simultaneously.</summary>
    public int MaxConcurrentSessions { get; init; } = 32;

    /// <summary>Maximum idle time in seconds before the watchdog reaps a session.</summary>
    public int MaxIdleSessionSeconds { get; init; } = 1800;

    /// <summary>Default shell executable when the caller does not specify one.</summary>
    public string DefaultShell { get; init; } = "/bin/sh";

    /// <summary>
    /// Allow-list of permitted shell executables.
    /// Any shell not in this list is rejected at session creation time.
    /// </summary>
    public string[] AllowedShells { get; init; } = ["/bin/sh", "/bin/bash", "python3", "ape"];

    /// <summary>Maximum number of output lines kept in the Redis ring-buffer per session.</summary>
    public int OutputRingBufferLines { get; init; } = 10_000;

    /// <summary>Default command timeout in seconds; individual requests may override this.</summary>
    public int CommandTimeoutSeconds { get; init; } = 600;

    /// <summary>When <see langword="true"/>, every command start and end is written to <c>shell_audit_log</c>.</summary>
    public bool AuditEnabled { get; init; } = true;

    /// <summary>CPU usage percentage cap applied via cgroup limits inside the container.</summary>
    public int SandboxCpuPercent { get; init; } = 80;

    /// <summary>Memory cap in megabytes applied via cgroup limits inside the container.</summary>
    public int SandboxMemoryMb { get; init; } = 2048;

    /// <summary>HTTP endpoint of this module reported during Block Controller registration.</summary>
    public string HttpEndpoint { get; init; } = "http://shell-vm:5950";

    /// <summary>WebSocket endpoint of this module reported during Block Controller registration.</summary>
    public string WsEndpoint { get; init; } = "ws://shell-vm:6950";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; init; } = "http://block-controller:5100";

    /// <summary>Redis connection string for session registry and output ring-buffer.</summary>
    public string RedisConnectionString { get; init; } = "redis:6379";

    /// <summary>PostgreSQL connection string for execution blocks and audit log.</summary>
    public string PostgresConnectionString { get; init; } =
        "Host=postgres;Port=5432;Database=mls_db;Username=mls_user;Password=mls";
}
