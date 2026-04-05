using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>AI_RESPONSE_COMPLETE</c> — final confirmation that all streaming
/// chunks have been delivered from ai-hub to the web-app.
/// </summary>
/// <param name="TotalChunks">Total number of <c>AI_RESPONSE_CHUNK</c> envelopes sent.</param>
/// <param name="ElapsedMs">Total wall-clock time in milliseconds from first token to completion.</param>
/// <param name="ProviderId">ID of the LLM provider that served this response (e.g. <c>"openai"</c>).</param>
/// <param name="ModelId">Model ID used for this response (e.g. <c>"gpt-4o"</c>).</param>
/// <param name="CanvasActionsDispatched">
/// Number of <c>AI_CANVAS_ACTION</c> envelopes dispatched during this response.
/// </param>
public sealed record AiResponseCompletePayload(
    [property: JsonPropertyName("total_chunks")] int TotalChunks,
    [property: JsonPropertyName("elapsed_ms")] long ElapsedMs,
    [property: JsonPropertyName("provider_id")] string ProviderId,
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("canvas_actions_dispatched")] int CanvasActionsDispatched);
