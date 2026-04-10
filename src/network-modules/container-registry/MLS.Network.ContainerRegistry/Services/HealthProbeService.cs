namespace MLS.Network.ContainerRegistry.Services;

/// <summary>Background service that probes registered images every 30 seconds.</summary>
public sealed class HealthProbeService(
    IContainerRegistryService _registry,
    IHttpClientFactory _httpFactory,
    ILogger<HealthProbeService> _logger) : BackgroundService
{
    // Reused across all probes to avoid socket exhaustion.
    private readonly HttpClient _probeClient = InitProbeClient(_httpFactory);

    private static HttpClient InitProbeClient(IHttpClientFactory factory)
    {
        var client = factory.CreateClient("health-probe");
        client.Timeout = TimeSpan.FromSeconds(5);
        return client;
    }
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
        // Only probe images that have an explicit health endpoint configured.
        if (string.IsNullOrWhiteSpace(img.HealthEndpoint))
        {
            _logger.LogDebug(
                "Skipping health probe for {Image}:{Tag} — no HealthEndpoint configured",
                img.Name, img.Tag);
            return;
        }

        if (!Uri.TryCreate(img.HealthEndpoint, UriKind.Absolute, out var probeUri) ||
            (probeUri.Scheme != Uri.UriSchemeHttp && probeUri.Scheme != Uri.UriSchemeHttps))
        {
            var invalid = new HealthCheckResult(img.Id, DateTimeOffset.UtcNow, false, 0,
                $"Invalid HealthEndpoint URI: {img.HealthEndpoint}");
            await _registry.RecordHealthCheckAsync(img.Id, invalid, ct).ConfigureAwait(false);
            _logger.LogWarning("Invalid HealthEndpoint URI for {Image}:{Tag}: {Uri}",
                img.Name, img.Tag, img.HealthEndpoint);
            return;
        }

        bool healthy;
        int statusCode;
        string message;
        try
        {
            using var response = await _probeClient.GetAsync(probeUri, ct).ConfigureAwait(false);
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
