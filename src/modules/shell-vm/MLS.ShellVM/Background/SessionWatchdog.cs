using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.ShellVM.Hubs;

namespace MLS.ShellVM.Background;

/// <summary>
/// Monitors sessions for timeout violations and reaps stale PTY processes.
/// Runs every 30 seconds; idle threshold is driven by <see cref="ShellVMConfig.MaxIdleSessionSeconds"/>.
/// </summary>
public sealed class SessionWatchdog(
    ISessionManager _sessions,
    IHubContext<ShellVMHub> _hub,
    IOptions<ShellVMConfig> _config,
    ILogger<SessionWatchdog> _logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(ShellVMLimits.WatchdogInterval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                await ReapIdleSessionsAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "SessionWatchdog scan failed");
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task ReapIdleSessionsAsync(CancellationToken ct)
    {
        var cfg          = _config.Value;
        var idleLimit    = TimeSpan.FromSeconds(cfg.MaxIdleSessionSeconds);
        var now          = DateTimeOffset.UtcNow;
        var toTerminate  = new List<(Guid Id, ShellSession Session)>();

        await foreach (var session in _sessions.GetSessionsAsync(filter: null, ct).ConfigureAwait(false))
        {
            // Only reap Running / Paused / Starting sessions; Created/Terminal states handled elsewhere
            if (session.Block.State is not (ExecutionBlockState.Running
                                         or ExecutionBlockState.Paused
                                         or ExecutionBlockState.Starting))
                continue;

            var lastActivity = session.Block.StartedAt ?? session.Block.CreatedAt;
            if (now - lastActivity > idleLimit)
                toTerminate.Add((session.Block.Id, session));
        }

        foreach (var (id, session) in toTerminate)
        {
            _logger.LogWarning(
                "Watchdog reaping idle session {Id} (label={Label}, idle>{IdleLimit})",
                id, session.Block.Label, idleLimit);

            try
            {
                var startedAt  = session.Block.StartedAt ?? session.Block.CreatedAt;
                var durationMs = (long)(now - startedAt).TotalMilliseconds;

                await _sessions.TerminateSessionAsync(id, graceful: false, ct)
                                .ConfigureAwait(false);

                var payload  = new ShellSessionTerminatedPayload(
                    Label:        session.Block.Label,
                    ExitCode:     null,
                    DurationMs:   durationMs,
                    TerminatedBy: "watchdog",
                    Timestamp:    now.ToString("O"));

                var envelope = EnvelopePayload.Create(
                    MessageTypes.ShellSessionTerminated,
                    ShellVMNetworkConstants.ModuleName,
                    payload);

                await _hub.Clients.Group("broadcast")
                          .SendAsync("ReceiveSessionTerminated", envelope, ct)
                          .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watchdog failed to terminate session {Id}", id);
            }
        }
    }
}
