#pragma warning disable SKEXP0010 // Custom OpenAI endpoint is experimental
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Providers;

/// <summary>
/// LLM provider for Groq — supports Llama3-70b and Mixtral via Groq's
/// OpenAI-compatible API (<c>https://api.groq.com/openai/v1</c>).
/// </summary>
public sealed class GroqProvider(
    IOptions<AIHubOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GroqProvider> logger) : ProviderBase(logger)
{
    private static readonly IReadOnlyList<string> Models = ["llama3-70b-8192", "mixtral-8x7b-32768"];

    /// <inheritdoc/>
    public override string ProviderId => "groq";

    /// <inheritdoc/>
    public override string DisplayName => "Groq";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => Models;

    /// <inheritdoc/>
    protected override async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var cfg = options.Value.Providers.Groq;
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
        ValidateModel(modelId);
        var cfg = options.Value.Providers.Groq;
        return new OpenAIChatCompletionService(
            modelId,
            apiKey: cfg.ApiKey,
            endpoint: new Uri(cfg.BaseUrl));
    }

    private void ValidateModel(string modelId)
    {
        if (!SupportedModels.Contains(modelId))
            throw new ArgumentException($"Model '{modelId}' is not supported by Groq provider.", nameof(modelId));
    }
}
#pragma warning restore SKEXP0010
