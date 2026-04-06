using MLS.Core.Contracts;

namespace MLS.WebApp.Services;

/// <summary>
/// Manages the SignalR connection to Block Controller and exposes typed event streams
/// for module status updates and envelope payloads.
/// </summary>
public interface IBlockControllerHub
{
    /// <summary>Establishes the underlying SignalR connection.</summary>
    Task ConnectAsync(CancellationToken ct);

    /// <summary>Disconnects cleanly from Block Controller.</summary>
    Task DisconnectAsync(CancellationToken ct);

    /// <summary>Returns <c>true</c> when the hub connection is open.</summary>
    bool IsConnected { get; }

    /// <summary>Streams filtered envelopes by topic list until cancellation.</summary>
    IAsyncEnumerable<EnvelopePayload> GetEnvelopeStreamAsync(string[] topics, CancellationToken ct);

    /// <summary>Streams module health / status updates until cancellation.</summary>
    IAsyncEnumerable<ModuleStatusUpdate> GetModuleUpdatesAsync(CancellationToken ct);
}

/// <summary>Live module status broadcast.</summary>
/// <param name="ModuleId">Registered module identifier.</param>
/// <param name="Status">Health state string (Healthy / Degraded / Offline).</param>
/// <param name="UptimeSeconds">Total uptime in seconds.</param>
/// <param name="CpuPercent">Current CPU usage 0–100.</param>
/// <param name="MemoryMb">Current RSS memory in MB.</param>
/// <param name="LastHeartbeat">UTC timestamp of the last heartbeat received.</param>
public sealed record ModuleStatusUpdate(
    string ModuleId,
    string Status,
    long UptimeSeconds,
    double CpuPercent,
    double MemoryMb,
    DateTimeOffset LastHeartbeat);
