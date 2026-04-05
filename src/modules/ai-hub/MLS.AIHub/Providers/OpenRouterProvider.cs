#pragma warning disable SKEXP0010 // Custom OpenAI endpoint is experimental
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Providers;

/// <summary>
/// LLM provider for OpenRouter — unified routing to 100+ models via an
/// OpenAI-compatible API (<c>https://openrouter.ai/api/v1</c>).
/// </summary>
public sealed class OpenRouterProvider(
    IOptions<AIHubOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<OpenRouterProvider> logger) : ProviderBase(logger)
{
    // Expose a representative set; OpenRouter supports 100+ models dynamically.
    private static readonly IReadOnlyList<string> Models =
    [
        "openai/gpt-4o",
        "anthropic/claude-3-5-sonnet",
        "google/gemini-2.5-pro",
        "meta-llama/llama-3-70b-instruct",
        "mistralai/mixtral-8x7b-instruct",
    ];

    /// <inheritdoc/>
    public override string ProviderId => "openrouter";

    /// <inheritdoc/>
    public override string DisplayName => "OpenRouter";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => Models;

    /// <inheritdoc/>
    protected override async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var cfg = options.Value.Providers.OpenRouter;
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            return false;

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cfg.ApiKey}");
        try
        {
            using var response = await client.GetAsync($"{cfg.BaseUrl}/models", ct).ConfigureAwait(false);
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
        var cfg = options.Value.Providers.OpenRouter;
        return new OpenAIChatCompletionService(
            modelId,
            apiKey: cfg.ApiKey,
            endpoint: new Uri(cfg.BaseUrl));
    }
}
#pragma warning restore SKEXP0010
