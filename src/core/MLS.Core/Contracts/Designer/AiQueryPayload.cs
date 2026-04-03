using System.Text.Json;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>AI_QUERY</c> — user message sent from web-app to ai-hub
/// together with assembled platform context.
/// </summary>
/// <param name="Query">The user's natural-language message.</param>
/// <param name="ProviderOverride">Optional: force a specific LLM provider (e.g. <c>"openai"</c>).</param>
/// <param name="ModelOverride">Optional: force a specific model (e.g. <c>"gpt-4o"</c>).</param>
/// <param name="IncludeCanvasContext">When <see langword="true"/>, current MDI layout is included in context.</param>
/// <param name="ConversationHistory">Prior turns for multi-turn conversations.</param>
public sealed record AiQueryPayload(
    string Query,
    string? ProviderOverride,
    string? ModelOverride,
    bool IncludeCanvasContext,
    IReadOnlyList<JsonElement> ConversationHistory);
