using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MLS.AIHub.Services;
using MLS.Core.Contracts.Designer;

namespace MLS.AIHub.Controllers;

/// <summary>
/// REST + SSE endpoint for the AI chat pipeline.
/// Provides both a bounded-queue POST endpoint (primary SignalR path)
/// and a GET SSE endpoint for HTTP-only clients.
/// </summary>
[ApiController]
[Route("api/chat")]
public sealed class ChatController(
    IChatRequestQueue _queue,
    IChatService _chatService,
    ILogger<ChatController> _logger) : ControllerBase
{
    // ── POST /api/chat ────────────────────────────────────────────────────────

    /// <summary>
    /// Accepts an AI query and enqueues it for background processing.
    /// Response chunks are delivered to the caller's SignalR group
    /// (<c>AI_RESPONSE_CHUNK</c> / <c>AI_CANVAS_ACTION</c> / <c>AI_RESPONSE_COMPLETE</c>).
    /// Returns HTTP 429 when the processing queue is at capacity.
    /// </summary>
    /// <param name="request">Query and optional conversation history.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>202 Accepted immediately; response arrives via SignalR.</returns>
    [HttpPost("")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult PostChat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "query must not be empty." });

        if (request.UserId == Guid.Empty)
            return BadRequest(new { error = "userId must be a valid non-empty GUID." });

        var payload = new AiQueryPayload(
            Query:                  request.Query,
            ProviderOverride:       request.ProviderOverride,
            ModelOverride:          request.ModelOverride,
            IncludeCanvasContext:   request.IncludeCanvasContext,
            ConversationHistory:    request.ConversationHistory ?? []);

        // Enqueue for background processing via bounded channel.
        // Pass ct so the queue processor can cancel if the HTTP caller disconnects first.
        var item = new ChatQueueItem(payload, request.UserId, ct);
        if (!_queue.TryEnqueue(item))
        {
            _logger.LogWarning(
                "ChatController: queue at capacity — returning 429 for userId {UserId}", request.UserId);
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Server busy; retry the request shortly." });
        }

        return Accepted(new { message = "Query accepted. Response streaming via SignalR." });
    }

    // ── GET /api/chat/stream ──────────────────────────────────────────────────

    /// <summary>
    /// Server-Sent Events (SSE) endpoint that streams AI response chunks directly
    /// over HTTP for clients that prefer SSE over SignalR.
    /// </summary>
    /// <param name="query">Natural-language query string.</param>
    /// <param name="userId">Caller's user identifier.</param>
    /// <param name="providerOverride">Optional provider override (e.g. <c>openai</c>).</param>
    /// <param name="modelOverride">Optional model override (e.g. <c>gpt-4o</c>).</param>
    /// <param name="ct">Cancellation token (client disconnect).</param>
    [HttpGet("stream")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task Stream(
        [FromQuery] string query,
        [FromQuery] Guid userId,
        [FromQuery] string? providerOverride = null,
        [FromQuery] string? modelOverride    = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "query must not be empty." }, ct)
                .ConfigureAwait(false);
            return;
        }

        if (userId == Guid.Empty)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "userId must be a valid non-empty GUID." }, ct)
                .ConfigureAwait(false);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection   = "keep-alive";

        var payload = new AiQueryPayload(
            Query:                query,
            ProviderOverride:     providerOverride,
            ModelOverride:        modelOverride,
            IncludeCanvasContext: false,
            ConversationHistory:  []);

        try
        {
            await foreach (var chunk in _chatService.StreamChunksAsync(payload, userId, ct)
                               .ConfigureAwait(false))
            {
                var data = JsonSerializer.Serialize(chunk);
                await Response.WriteAsync($"data: {data}\n\n", ct).ConfigureAwait(false);
                await Response.Body.FlushAsync(ct).ConfigureAwait(false);

                if (chunk.IsFinal) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal; no action needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ChatController: error streaming response for userId {UserId}", userId);

            if (!Response.HasStarted)
            {
                Response.StatusCode = StatusCodes.Status500InternalServerError;
                await Response.WriteAsync($"data: {{\"error\":\"Internal server error\"}}\n\n", ct)
                    .ConfigureAwait(false);
            }
        }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>Body for <c>POST /api/chat</c>.</summary>
/// <param name="Query">User's natural-language message.</param>
/// <param name="UserId">Caller's user identifier — used to route SignalR response chunks.</param>
/// <param name="ProviderOverride">Optional: force a specific LLM provider.</param>
/// <param name="ModelOverride">Optional: force a specific model.</param>
/// <param name="IncludeCanvasContext">Whether to include the MDI canvas layout in context.</param>
/// <param name="ConversationHistory">Prior conversation turns for multi-turn support.</param>
public sealed record ChatRequest(
    string Query,
    Guid UserId,
    string? ProviderOverride = null,
    string? ModelOverride = null,
    bool IncludeCanvasContext = false,
    IReadOnlyList<System.Text.Json.JsonElement>? ConversationHistory = null);
