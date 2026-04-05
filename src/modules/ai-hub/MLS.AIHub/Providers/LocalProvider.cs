#pragma warning disable SKEXP0010 // Custom OpenAI endpoint is experimental
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Providers;

/// <summary>
/// LLM provider for local inference via Ollama or llama.cpp (OpenAI-compatible API).
/// This is the <em>always-available</em> final fallback in the provider chain.
/// </summary>
public sealed class LocalProvider(
    IOptions<AIHubOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<LocalProvider> logger) : ProviderBase(logger)
{
    private static readonly IReadOnlyList<string> Models = ["llama3", "llama3:70b", "mistral", "codellama"];

    /// <inheritdoc/>
    public override string ProviderId => "local";

    /// <inheritdoc/>
    public override string DisplayName => "Local (Ollama)";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => Models;

    /// <inheritdoc/>
    public override bool IsAvailable => true; // Local is always considered available as final fallback

    /// <inheritdoc/>
    protected override async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var cfg = options.Value.Providers.Local;

        using var client = httpClientFactory.CreateClient();
        try
        {
            // Ollama health check endpoint
            using var response = await client.GetAsync($"{cfg.OllamaBaseUrl}/api/tags", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // Ollama may not be running — that's acceptable for local dev
            return false;
        }
    }

    /// <inheritdoc/>
    public override IChatCompletionService BuildService(string modelId)
    {
        var cfg = options.Value.Providers.Local;
        var effectiveModel = ResolveModel(modelId, cfg);
        // Ollama exposes an OpenAI-compatible /v1 endpoint
        return new OpenAIChatCompletionService(
            effectiveModel,
            apiKey: "ollama", // Ollama ignores the key but SK requires a non-empty value
            endpoint: new Uri($"{cfg.OllamaBaseUrl}/v1"));
    }

    private string ResolveModel(string modelId, LocalConfig cfg)
        => SupportedModels.Contains(modelId) ? modelId : cfg.DefaultModel;
}
#pragma warning restore SKEXP0010
