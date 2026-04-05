using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Providers;

/// <summary>
/// LLM provider for OpenAI — supports GPT-4o, GPT-4-turbo, and o3.
/// Uses the official Semantic Kernel OpenAI connector.
/// </summary>
public sealed class OpenAIProvider(
    IOptions<AIHubOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAIProvider> logger) : ProviderBase(logger)
{
    private static readonly IReadOnlyList<string> Models = ["gpt-4o", "gpt-4-turbo", "o3"];

    /// <inheritdoc/>
    public override string ProviderId => "openai";

    /// <inheritdoc/>
    public override string DisplayName => "OpenAI";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => Models;

    /// <inheritdoc/>
    protected override async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var apiKey = options.Value.Providers.OpenAI.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        try
        {
            using var response = await client
                .GetAsync("https://api.openai.com/v1/models", ct)
                .ConfigureAwait(false);
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
        return new OpenAIChatCompletionService(modelId, options.Value.Providers.OpenAI.ApiKey);
    }

    private void ValidateModel(string modelId)
    {
        if (!SupportedModels.Contains(modelId))
            throw new ArgumentException($"Model '{modelId}' is not supported by OpenAI provider.", nameof(modelId));
    }
}
