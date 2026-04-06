using System.Net.Http.Json;
using System.Text.Json;
using MLS.Core.Constants;
using MLS.Core.Contracts;

namespace MLS.WebApp.Services;

/// <summary>
/// Background service that registers the web-app module with Block Controller on startup
/// and sends MODULE_HEARTBEAT every 5 seconds.
/// </summary>
public sealed class BlockControllerClient(
    HttpClient httpClient,
    ILogger<BlockControllerClient> logger) : BackgroundService
{
    private static readonly string ModuleId = "web-app";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RegisterAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SendHeartbeatAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RegisterAsync(CancellationToken ct)
    {
        try
        {
            var envelope = EnvelopePayload.Create(
                MessageTypes.ModuleRegister,
                ModuleId,
                new { module_id = ModuleId, http_port = 5200, ws_port = 6200 });

            await httpClient.PostAsJsonAsync("/api/modules/register", envelope, ct)
                .ConfigureAwait(false);

            logger.LogInformation("web-app registered with Block Controller");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to register with Block Controller — will retry on next heartbeat");
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            var envelope = EnvelopePayload.Create(
                MessageTypes.ModuleHeartbeat,
                ModuleId,
                new { module_id = ModuleId, timestamp = DateTimeOffset.UtcNow });

            await httpClient.PostAsJsonAsync("/api/modules/heartbeat", envelope, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Heartbeat to Block Controller failed");
        }
    }
}
