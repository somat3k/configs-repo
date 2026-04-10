using System.Runtime.CompilerServices;

namespace MLS.Network.VirtualMachine.Services;

/// <summary>State of a sandbox execution instance.</summary>
public enum SandboxState { Pending, Running, Completed, Failed, TimedOut }

/// <summary>Request payload for sandbox execution.</summary>
public sealed record SandboxRequest(
    string Script,
    int TimeoutSeconds = 30,
    int MemoryLimitMb = 64,
    int MaxOutputBytes = 65536);

/// <summary>Result of a sandbox execution.</summary>
public sealed record SandboxResult(
    Guid SandboxId,
    bool Success,
    string Output,
    string? Error,
    long DurationMs,
    int ExitCode);

/// <summary>Metadata about an active or completed sandbox instance.</summary>
public sealed record SandboxInfo(
    Guid SandboxId,
    SandboxState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Service interface for sandboxed C# script execution.</summary>
public interface IVirtualMachineService
{
    /// <summary>Executes a C# script in an isolated sandbox.</summary>
    Task<SandboxResult> ExecuteAsync(SandboxRequest request, CancellationToken ct);

    /// <summary>Gets info about a specific sandbox instance.</summary>
    Task<SandboxInfo?> GetSandboxAsync(Guid sandboxId, CancellationToken ct);

    /// <summary>Streams all active sandbox instances.</summary>
    IAsyncEnumerable<SandboxInfo> GetActiveSandboxesAsync(CancellationToken ct);

    /// <summary>Terminates and removes a sandbox by ID.</summary>
    Task TerminateSandboxAsync(Guid sandboxId, CancellationToken ct);
}
