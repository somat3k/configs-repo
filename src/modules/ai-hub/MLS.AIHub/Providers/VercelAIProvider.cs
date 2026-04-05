#pragma warning disable SKEXP0010 // Custom OpenAI endpoint is experimental
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Providers;

/// <summary>
/// LLM provider for Vercel AI SDK compatible edge endpoints.
/// Calls the configured <c>VercelAI.BaseUrl</c> using the OpenAI-compatible
/// wire format exposed by Vercel AI's edge functions.
/// </summary>
public sealed class VercelAIProvider(
    IOptions<AIHubOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<VercelAIProvider> logger) : ProviderBase(logger)
{
    private static readonly IReadOnlyList<string> Models = ["vercel-ai-default"];

    /// <inheritdoc/>
    public override string ProviderId => "vercelai";

    /// <inheritdoc/>
    public override string DisplayName => "Vercel AI";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => Models;

    /// <inheritdoc/>
    protected override async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var cfg = options.Value.Providers.VercelAI;
        if (string.IsNullOrWhiteSpace(cfg.BaseUrl))
            return false;

        using var client = httpClientFactory.CreateClient();
        if (!string.IsNullOrWhiteSpace(cfg.ApiKey))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cfg.ApiKey}");

        try
        {
            using var response = await client.GetAsync(cfg.BaseUrl, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public override IChatCompletionService BuildService(string modelId)
    {
        var cfg = options.Value.Providers.VercelAI;
        EnsureConfigured(cfg);
        return new OpenAIChatCompletionService(
            modelId,
            apiKey: cfg.ApiKey,
            endpoint: new Uri(cfg.BaseUrl));
    }

    private static void EnsureConfigured(VercelAIConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.BaseUrl))
            throw new InvalidOperationException("VercelAI.BaseUrl must be configured before calling BuildService.");
    }
}
#pragma warning restore SKEXP0010
