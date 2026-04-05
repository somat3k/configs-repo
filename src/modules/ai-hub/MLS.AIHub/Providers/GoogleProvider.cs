#pragma warning disable SKEXP0010 // Custom OpenAI endpoint is experimental
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Providers;

/// <summary>
/// LLM provider for Google AI — supports Gemini 2.5 Pro and Gemini Flash.
/// Uses the Gemini OpenAI-compatible REST endpoint so no extra SK connector package is required.
/// </summary>
public sealed class GoogleProvider(
    IOptions<AIHubOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GoogleProvider> logger) : ProviderBase(logger)
{
    private static readonly IReadOnlyList<string> Models = ["gemini-2.5-pro", "gemini-2.0-flash"];
    private const string GeminiOpenAIBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai";

    /// <inheritdoc/>
    public override string ProviderId => "google";

    /// <inheritdoc/>
    public override string DisplayName => "Google AI";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => Models;

    /// <inheritdoc/>
    protected override async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var apiKey = options.Value.Providers.Google.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        using var client = httpClientFactory.CreateClient();
        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
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
        // Gemini exposes an OpenAI-compatible endpoint — use SK's OpenAI connector
        return new OpenAIChatCompletionService(
            modelId,
            apiKey: options.Value.Providers.Google.ApiKey,
            endpoint: new Uri(GeminiOpenAIBaseUrl));
    }

    private void ValidateModel(string modelId)
    {
        if (!SupportedModels.Contains(modelId))
            throw new ArgumentException($"Model '{modelId}' is not supported by Google provider.", nameof(modelId));
    }
}
#pragma warning restore SKEXP0010
