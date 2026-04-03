using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Designer.Configuration;

namespace MLS.Designer.Services;

/// <summary>
/// Handles MODULE_REGISTER on startup and MODULE_HEARTBEAT every 5 seconds
/// via the Block Controller REST API.
/// </summary>
public sealed class BlockControllerClient(
    HttpClient _http,
    IOptions<DesignerOptions> _options,
    ILogger<BlockControllerClient> _logger) : IHostedService, IDisposable
{
    private const string ModuleId   = "designer";
    private const string ModuleName = "designer";

    private Timer? _heartbeatTimer;
    private Guid   _registeredId;

    // ── IHostedService ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct)
    {
        await RegisterAsync(ct).ConfigureAwait(false);
        _heartbeatTimer = new Timer(
            callback: _ =>
            {
                try
                {
                    SendHeartbeatAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Heartbeat timer callback failed");
                }
            },
            state:    null,
            dueTime:  TimeSpan.FromSeconds(5),
            period:   TimeSpan.FromSeconds(5));
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct)
    {
        if (_heartbeatTimer is not null)
            await _heartbeatTimer.DisposeAsync().ConfigureAwait(false);

        await DeregisterAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => _heartbeatTimer?.Dispose();

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task RegisterAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        var request = new
        {
            module_name   = ModuleName,
            endpoint_http = opts.HttpEndpoint,
            endpoint_ws   = opts.WsEndpoint,
            capabilities  = new[] { "block-graph", "strategy-editor", "indicator", "ml-inference" },
            version       = "1.0.0",
        };

        try
        {
            var response = await _http.PostAsJsonAsync("/api/modules/register", request, ct)
                                      .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var reg = await response.Content.ReadFromJsonAsync<RegistrationResponse>(ct)
                                             .ConfigureAwait(false);
            if (reg is not null)
            {
                _registeredId = reg.ModuleId;
                _logger.LogInformation("Designer registered with Block Controller as {Id}", _registeredId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register with Block Controller — will retry on next heartbeat");
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        var envelope = EnvelopePayload.Create(
            MessageTypes.ModuleHeartbeat,
            ModuleId,
            new { status = "healthy", module = ModuleName, timestamp = DateTimeOffset.UtcNow });

        try
        {
            var response = await _http.PostAsJsonAsync("/api/modules/heartbeat", envelope, ct)
                                      .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Heartbeat rejected: {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat failed — Block Controller may be unavailable");
        }
    }

    private async Task DeregisterAsync(CancellationToken ct)
    {
        if (_registeredId == Guid.Empty)
            return;

        try
        {
            await _http.DeleteAsync($"/api/modules/{_registeredId}", ct).ConfigureAwait(false);
            _logger.LogInformation("Designer deregistered from Block Controller");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deregister call failed — Block Controller may be unavailable");
        }
    }

    // ── Nested types ──────────────────────────────────────────────────────────────

    private sealed record RegistrationResponse(Guid ModuleId);
}
