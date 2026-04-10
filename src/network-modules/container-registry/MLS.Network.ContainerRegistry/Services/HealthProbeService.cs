namespace MLS.Network.ContainerRegistry.Services;

/// <summary>Background service that probes registered images every 30 seconds.</summary>
public sealed class HealthProbeService(
    IContainerRegistryService _registry,
    IHttpClientFactory _httpFactory,
    ILogger<HealthProbeService> _logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var images = new List<ContainerImage>();
            await foreach (var img in _registry.ListImagesAsync(ct).ConfigureAwait(false))
                images.Add(img);

            foreach (var img in images)
            {
                ct.ThrowIfCancellationRequested();
                await ProbeImageAsync(img, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ProbeImageAsync(ContainerImage img, CancellationToken ct)
    {
        var url = $"http://{img.Registry}/{img.Name}:{img.Tag}/health";
        bool healthy;
        int statusCode;
        string message;
        try
        {
            using var client = _httpFactory.CreateClient();
            client.Timeout   = TimeSpan.FromSeconds(5);
            var response     = await client.GetAsync(url, ct).ConfigureAwait(false);
            healthy    = response.IsSuccessStatusCode;
            statusCode = (int)response.StatusCode;
            message    = response.ReasonPhrase ?? string.Empty;
        }
        catch (Exception ex)
        {
            healthy    = false;
            statusCode = 0;
            message    = ex.Message;
        }

        var result = new HealthCheckResult(img.Id, DateTimeOffset.UtcNow, healthy, statusCode, message);
        await _registry.RecordHealthCheckAsync(img.Id, result, ct).ConfigureAwait(false);
        _logger.LogDebug("Health probe {Image}:{Tag} → {Healthy}", img.Name, img.Tag, healthy);
    }
}
