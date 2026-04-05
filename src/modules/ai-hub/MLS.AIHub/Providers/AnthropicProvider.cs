using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Providers;

/// <summary>
/// LLM provider for Anthropic — supports Claude 3.5 Sonnet and Claude 3 Opus.
/// Implements a custom <see cref="IChatCompletionService"/> backed by Anthropic's
/// Messages REST API because no official Semantic Kernel connector exists.
/// </summary>
public sealed class AnthropicProvider(
    IOptions<AIHubOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<AnthropicProvider> logger) : ProviderBase(logger)
{
    private static readonly IReadOnlyList<string> Models = ["claude-3-5-sonnet-20241022", "claude-3-opus-20240229"];

    /// <inheritdoc/>
    public override string ProviderId => "anthropic";

    /// <inheritdoc/>
    public override string DisplayName => "Anthropic";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => Models;

    /// <inheritdoc/>
    protected override async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var cfg = options.Value.Providers.Anthropic;
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            return false;

        using var client = CreateHttpClient(cfg, httpClientFactory);
        try
        {
            using var response = await client.GetAsync($"{cfg.BaseUrl}/v1/models", ct).ConfigureAwait(false);
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
        return new AnthropicChatCompletionService(modelId, options.Value.Providers.Anthropic, httpClientFactory, logger);
    }

    private static HttpClient CreateHttpClient(AnthropicConfig cfg, IHttpClientFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", cfg.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        return client;
    }

    private void ValidateModel(string modelId)
    {
        if (!SupportedModels.Contains(modelId))
            throw new ArgumentException($"Model '{modelId}' is not supported by Anthropic provider.", nameof(modelId));
    }
}

/// <summary>
/// Semantic Kernel adapter that calls Anthropic's Messages API.
/// Implements <see cref="IChatCompletionService"/> which in SK 1.74+ covers
/// both batch and streaming completions.
/// </summary>
internal sealed class AnthropicChatCompletionService(
    string modelId,
    AnthropicConfig config,
    IHttpClientFactory httpClientFactory,
    ILogger logger) : IChatCompletionService
{
    private const string MessagesEndpoint = "/v1/messages";

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?> { ["ModelId"] = modelId };

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var body = BuildRequestBody(chatHistory, stream: false);
        using var client = CreateClient();

        using var response = await client
            .PostAsJsonAsync($"{config.BaseUrl}{MessagesEndpoint}", body, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<AnthropicMessageResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var text = result?.Content?.FirstOrDefault(c => c.Type == "text")?.Text ?? string.Empty;
        return [new ChatMessageContent(AuthorRole.Assistant, text)];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var body = BuildRequestBody(chatHistory, stream: true);
        using var client = CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}{MessagesEndpoint}")
        {
            Content = JsonContent.Create(body),
        };

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var json = line["data:".Length..].Trim();
            if (json == "[DONE]")
                break;

            AnthropicStreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<AnthropicStreamEvent>(json);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse Anthropic stream event");
                continue;
            }

            if (evt?.Type == "content_block_delta" && evt.Delta?.Type == "text_delta")
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, evt.Delta.Text);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private object BuildRequestBody(ChatHistory chatHistory, bool stream)
    {
        var messages = chatHistory
            .Where(m => m.Role != AuthorRole.System)
            .Select(m => new { role = m.Role == AuthorRole.User ? "user" : "assistant", content = m.Content })
            .ToList();

        var systemMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System)?.Content ?? string.Empty;

        return new
        {
            model = modelId,
            max_tokens = 8192,
            system = systemMessage,
            messages,
            stream,
        };
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    // ── Response models ───────────────────────────────────────────────────────

    private sealed record AnthropicMessageResponse(
        [property: JsonPropertyName("content")] List<AnthropicContent>? Content);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private sealed record AnthropicStreamEvent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("delta")] AnthropicDelta? Delta);

    private sealed record AnthropicDelta(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);
}
