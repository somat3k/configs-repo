using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MLS.DataLayer.Configuration;

namespace MLS.DataLayer.Services;

/// <summary>
/// Hosted background service that registers the Data Layer module with Block Controller
/// (MODULE_REGISTER) on startup and sends MODULE_HEARTBEAT every 5 seconds.
/// Automatically retries registration if the first attempt fails.
/// </summary>
public sealed class BlockControllerClient(
    HttpClient _http,
    IOptions<DataLayerOptions> _options,
    ILogger<BlockControllerClient> _logger) : BackgroundService
{
    private const string ModuleName = "data-layer";

    private Guid _registeredId;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RegisterAsync(ct).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            if (_registeredId == Guid.Empty)
                await RegisterAsync(ct).ConfigureAwait(false);
            else
                await SendHeartbeatAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct).ConfigureAwait(false);
        await DeregisterAsync(CancellationToken.None).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task RegisterAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        var request = new
        {
            module_name   = ModuleName,
            endpoint_http = opts.HttpEndpoint,
            endpoint_ws   = opts.WsEndpoint,
            capabilities  = new[] { "feed-collection", "gap-detection", "backfill", "candle-store" },
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
                _logger.LogInformation("DataLayer registered with Block Controller as {Id}", _registeredId);
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
                    $"/api/modules/{_registeredId}/heartbeat", content: null, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Heartbeat rejected: {Status} — will re-register on next tick", response.StatusCode);
                _registeredId = Guid.Empty;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Heartbeat failed — Block Controller may be unavailable");
        }
    }

    private async Task DeregisterAsync(CancellationToken ct)
    {
        if (_registeredId == Guid.Empty) return;

        try
        {
            await _http.DeleteAsync($"/api/modules/{_registeredId}", ct).ConfigureAwait(false);
            _logger.LogInformation("DataLayer deregistered from Block Controller");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deregister call failed");
        }
    }

    private sealed record RegistrationResponse(
        [property: JsonPropertyName("module_id")] Guid ModuleId);
}
