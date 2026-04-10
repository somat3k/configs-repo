namespace MLS.ShellVM.Background;

/// <summary>
/// Sends <c>MODULE_HEARTBEAT</c> to Block Controller every 5 seconds.
/// The heartbeat payload includes active session count and process metrics.
/// </summary>
/// <remarks>
/// Metric collection is best-effort; failures are logged as warnings and do not
/// interrupt the heartbeat loop.
/// </remarks>
public sealed class HeartbeatService(
    ISessionManager _sessions,
    ILogger<HeartbeatService> _logger) : BackgroundService
{
    // The actual HTTP call is handled by BlockControllerClient (the typed HttpClient-based
    // BackgroundService). This service owns per-tick metric collection and can be extended
    // to emit additional telemetry without coupling the registration logic.

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                var activeSessions = _sessions.ActiveSessionCount;
                _logger.LogDebug("[Heartbeat] active_sessions={Sessions}", activeSessions);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "HeartbeatService metric collection failed");
            }
        }
    }
}
