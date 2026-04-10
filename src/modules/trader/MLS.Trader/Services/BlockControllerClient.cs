using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MLS.Trader.Configuration;

namespace MLS.Trader.Services;

/// <summary>
/// Hosted background service that registers the Trader module with Block Controller
/// (<c>MODULE_REGISTER</c>) on startup and sends <c>MODULE_HEARTBEAT</c> every 5 seconds.
/// Automatically retries registration when the Block Controller is unavailable.
/// </summary>
public sealed class BlockControllerClient(
    HttpClient _http,
    ModuleIdentity _identity,
    IOptions<TraderOptions> _options,
    ILogger<BlockControllerClient> _logger) : BackgroundService
{
    private const string ModuleName = "trader";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RegisterAsync(ct).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            if (_identity.Id == Guid.Empty)
                await RegisterAsync(ct).ConfigureAwait(false);
            else
                await SendHeartbeatAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct).ConfigureAwait(false);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await DeregisterAsync(cts.Token).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task RegisterAsync(CancellationToken ct)
    {
        var opts    = _options.Value;
        var request = new
        {
            module_name   = ModuleName,
            endpoint_http = opts.HttpEndpoint,
            endpoint_ws   = opts.WsEndpoint,
            capabilities  = new[] { "signal-generation", "order-management", "risk-management" },
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
                _identity.Id = reg.ModuleId;
                _logger.LogInformation("Trader registered with Block Controller as {Id}", _identity.Id);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not register with Block Controller — will retry on next tick");
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.PatchAsync(
                    $"/api/modules/{_identity.Id}/heartbeat", content: null, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Heartbeat rejected: {Status} — will re-register on next tick", response.StatusCode);
                _identity.Id = Guid.Empty;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Heartbeat failed — Block Controller may be unavailable");
        }
    }

    private async Task DeregisterAsync(CancellationToken ct)
    {
        if (_identity.Id == Guid.Empty) return;

        try
        {
            await _http.DeleteAsync($"/api/modules/{_identity.Id}", ct).ConfigureAwait(false);
            _logger.LogInformation("Trader deregistered from Block Controller");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deregister call failed");
        }
    }

    private sealed record RegistrationResponse(
        [property: JsonPropertyName("module_id")] Guid ModuleId);
}
