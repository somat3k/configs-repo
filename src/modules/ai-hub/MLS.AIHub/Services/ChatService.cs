using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MLS.AIHub.Canvas;
using MLS.AIHub.Context;
using MLS.AIHub.Hubs;
using MLS.AIHub.Plugins;
using MLS.AIHub.Providers;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using AIHubHub = MLS.AIHub.Hubs.AIHub;

namespace MLS.AIHub.Services;

/// <summary>
/// Contract for the AI streaming chat pipeline.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Processes an <c>AI_QUERY</c>: assembles context, invokes SK with streaming,
    /// dispatches canvas actions, and forwards all chunks to the user's SignalR group.
    /// Always sends <c>AI_RESPONSE_COMPLETE</c> regardless of success or failure.
    /// </summary>
    /// <param name="query">Incoming query payload.</param>
    /// <param name="userId">Requesting user / client identifier — routes to their SignalR group.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessQueryAsync(AiQueryPayload query, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns an <see cref="IAsyncEnumerable{T}"/> of response chunks for SSE / HTTP streaming clients.
    /// Canvas actions are still dispatched as a side-effect.
    /// Always yields a terminal chunk with <c>IsFinal=true</c>, even when streaming fails.
    /// </summary>
    /// <param name="query">Incoming query payload.</param>
    /// <param name="userId">Requesting user / client identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<AiResponseChunkPayload> StreamChunksAsync(
        AiQueryPayload query, Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Streaming chat pipeline: AI_QUERY → context assembly → provider selection →
/// SK auto-invoke → <c>AI_RESPONSE_CHUNK</c> / <c>AI_CANVAS_ACTION</c> / <c>AI_RESPONSE_COMPLETE</c>
/// pushed to the user's SignalR group.
/// </summary>
/// <remarks>
/// Canvas actions are dispatched by the individual SK plugins themselves
/// (via <see cref="ICanvasActionDispatcher"/>) before returning their string result —
/// so they arrive at the web-app in parallel with the streaming text.
/// </remarks>
public sealed class ChatService(
    IContextAssembler _context,
    IProviderRouter _router,
    IHubContext<AIHubHub> _hub,
    ICanvasActionCounter _canvasCounter,
    TradingPlugin _trading,
    DesignerPlugin _designer,
    AnalyticsPlugin _analytics,
    MLRuntimePlugin _mlRuntime,
    DeFiPlugin _defi,
    ILogger<ChatService> _logger) : IChatService
{
    private const string ModuleId = "ai-hub";

    // ── IChatService ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task ProcessQueryAsync(AiQueryPayload query, Guid userId, CancellationToken ct = default)
    {
        var sw         = Stopwatch.StartNew();
        var providerId = "unknown";
        var modelId    = "unknown";
        var chunkIndex = 0;
        // When true the finally block sends AI_RESPONSE_COMPLETE.
        // Set to false only when the caller explicitly cancels — the connection may already be gone.
        var sendComplete = true;

        try
        {
            var provider = await SelectProviderAsync(query, userId, ct).ConfigureAwait(false);
            providerId = provider.ProviderId;
            modelId    = ResolveModelId(query, provider);

            var chatSvc  = provider.BuildService(modelId);
            var snapshot = await _context.AssembleAsync(userId, ct).ConfigureAwait(false);
            var history  = BuildChatHistory(query, snapshot);
            var kernel   = BuildKernel(chatSvc);

            await foreach (var chunk in InnerStreamAsync(chatSvc, history, kernel, ct).ConfigureAwait(false))
            {
                var payload  = new AiResponseChunkPayload(chunkIndex++, chunk, false, 0);
                var envelope = EnvelopePayload.Create(MessageTypes.AiResponseChunk, ModuleId, payload);

                await _hub.Clients.Group(userId.ToString())
                    .SendAsync("ReceiveEnvelope", envelope, ct)
                    .ConfigureAwait(false);
            }

            _logger.LogInformation(
                "ChatService: response complete — {Chunks} chunks, {Elapsed}ms, provider={Provider}, model={Model}",
                chunkIndex, sw.ElapsedMilliseconds, providerId,
                // Sanitise user-supplied model override to prevent log-forging
                modelId.Replace('\r', '_').Replace('\n', '_'));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled (e.g. connection dropped) — skip terminal envelope;
            // the recipient group may no longer be listening.
            sendComplete = false;
            _logger.LogInformation("ChatService: query cancelled for userId {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatService: error processing query for userId {UserId}", userId);
            // sendComplete remains true — fall through to finally to send the terminal signal.
        }
        finally
        {
            if (sendComplete)
            {
                sw.Stop();
                var completePayload  = new AiResponseCompletePayload(
                    chunkIndex, sw.ElapsedMilliseconds, providerId, modelId, _canvasCounter.Count);
                var completeEnvelope = EnvelopePayload.Create(
                    MessageTypes.AiResponseComplete, ModuleId, completePayload);

                // CancellationToken.None — always deliver the terminal envelope,
                // even when ct was cancelled or a streaming error occurred.
                await _hub.Clients.Group(userId.ToString())
                    .SendAsync("ReceiveEnvelope", completeEnvelope, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AiResponseChunkPayload> StreamChunksAsync(
        AiQueryPayload query, Guid userId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var provider = await SelectProviderAsync(query, userId, ct).ConfigureAwait(false);
        var modelId  = ResolveModelId(query, provider);
        var chatSvc  = provider.BuildService(modelId);
        var snapshot = await _context.AssembleAsync(userId, ct).ConfigureAwait(false);
        var history  = BuildChatHistory(query, snapshot);
        var kernel   = BuildKernel(chatSvc);

        int chunkIndex  = 0;
        string? errorMsg = null;

        // WrapStreamAsync catches non-cancellation exceptions from InnerStreamAsync,
        // ensuring the iterator always reaches the terminal yield below.
        await foreach (var text in WrapStreamAsync(
                InnerStreamAsync(chatSvc, history, kernel, ct),
                ex => errorMsg = "Streaming interrupted; please retry.",
                ct)
            .ConfigureAwait(false))
        {
            yield return new AiResponseChunkPayload(chunkIndex++, text, false, 0);
        }

        // Terminal chunk — always yielded (includes error hint when streaming failed)
        yield return new AiResponseChunkPayload(chunkIndex, errorMsg ?? string.Empty, true, 0);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Drives SK streaming and yields non-empty text fragments.</summary>
    private async IAsyncEnumerable<string> InnerStreamAsync(
        IChatCompletionService chatSvc,
        ChatHistory history,
        Kernel kernel,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };

        await foreach (var chunk in chatSvc
            .GetStreamingChatMessageContentsAsync(history, settings, kernel, ct)
            .ConfigureAwait(false))
        {
            var text = chunk.Content;
            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    /// <summary>
    /// Wraps <paramref name="source"/> to catch non-cancellation exceptions, invoking
    /// <paramref name="onError"/> and stopping iteration cleanly so the caller can
    /// yield a terminal chunk. This pattern avoids placing <c>yield return</c> inside
    /// a try/catch block (a C# compiler restriction).
    /// </summary>
    private async IAsyncEnumerable<string> WrapStreamAsync(
        IAsyncEnumerable<string> source,
        Action<Exception> onError,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);

        while (true)
        {
            // --- try/catch block: sets flags but does NOT contain yield ---
            string? value  = null;
            bool    hasNext = false;
            bool    stop    = false;

            try
            {
                hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                if (hasNext) value = enumerator.Current;
            }
            catch (OperationCanceledException)
            {
                stop = true;   // ct cancelled — stop cleanly
            }
            catch (Exception ex)
            {
                onError(ex);
                _logger.LogError(ex, "ChatService.WrapStreamAsync: streaming error");
                stop = true;
            }
            // -------------------------------------------------------------------

            if (stop || !hasNext) break;

            yield return value!;
        }
    }

    private async Task<ILLMProvider> SelectProviderAsync(
        AiQueryPayload query, Guid userId, CancellationToken ct)
    {
        try
        {
            return await _router.SelectProviderAsync(query, userId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatService: provider selection failed for userId {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Resolves the model to use, validating any override against the provider's
    /// declared <see cref="ILLMProvider.SupportedModels"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Provider has no supported models.</exception>
    /// <exception cref="ArgumentException">Model override is not in the provider's list.</exception>
    private static string ResolveModelId(AiQueryPayload query, ILLMProvider provider)
    {
        if (provider.SupportedModels.Count == 0)
            throw new InvalidOperationException(
                $"Provider '{provider.ProviderId}' reports no supported models.");

        if (!string.IsNullOrWhiteSpace(query.ModelOverride))
        {
            foreach (var supported in provider.SupportedModels)
            {
                if (string.Equals(supported, query.ModelOverride, StringComparison.OrdinalIgnoreCase))
                    return supported;
            }

            throw new ArgumentException(
                $"Model override '{query.ModelOverride}' is not supported by provider " +
                $"'{provider.ProviderId}'. Supported models: {string.Join(", ", provider.SupportedModels)}.",
                nameof(query));
        }

        return provider.SupportedModels[0];
    }

    /// <summary>Builds a fresh <see cref="Kernel"/> scoped to this request.</summary>
    private Kernel BuildKernel(IChatCompletionService chatSvc)
    {
        var kb = Kernel.CreateBuilder();
        kb.Services.AddSingleton(chatSvc);
        var kernel = kb.Build();

        kernel.Plugins.AddFromObject(_trading,   "Trading");
        kernel.Plugins.AddFromObject(_designer,  "Designer");
        kernel.Plugins.AddFromObject(_analytics, "Analytics");
        kernel.Plugins.AddFromObject(_mlRuntime, "MLRuntime");
        kernel.Plugins.AddFromObject(_defi,      "DeFi");

        return kernel;
    }

    /// <summary>
    /// Constructs a <see cref="ChatHistory"/> with a system prompt containing the
    /// live platform snapshot, followed by prior conversation turns and the user's query.
    /// </summary>
    private static ChatHistory BuildChatHistory(AiQueryPayload query, ProjectSnapshot snapshot)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(BuildSystemPrompt(snapshot));

        // Guard against null ConversationHistory (possible when the JSON field is omitted)
        if (query.ConversationHistory is not null)
        {
            foreach (var turn in query.ConversationHistory)
            {
                if (!turn.TryGetProperty("role", out var roleEl) ||
                    !turn.TryGetProperty("content", out var contentEl))
                    continue;

                var role    = roleEl.GetString();
                var content = contentEl.GetString() ?? string.Empty;

                if (role == "user")           history.AddUserMessage(content);
                else if (role == "assistant") history.AddAssistantMessage(content);
            }
        }

        history.AddUserMessage(query.Query);
        return history;
    }

    private static string BuildSystemPrompt(ProjectSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an AI assistant deeply integrated with MLS (Machine Learning Studio),");
        sb.AppendLine("a professional trading, arbitrage, and DeFi platform running on Arbitrum.");
        sb.AppendLine("You have direct access to live trading data, open positions, DeFi health factors,");
        sb.AppendLine("trained ML models, strategy graphs, and real-time market signals via your tools.");
        sb.AppendLine();
        sb.AppendLine("## Current Platform State");
        sb.AppendLine($"- Snapshot assembled in {snapshot.AssemblyMs}ms at {snapshot.AssembledAt:u}");
        sb.AppendLine($"- Registered modules: {snapshot.Modules.Count}");
        sb.AppendLine($"- Open positions: {snapshot.OpenPositions.Count}");
        sb.AppendLine($"- Active strategies: {snapshot.ActiveStrategies.Count}");
        sb.AppendLine($"- ML models: {snapshot.MlModels.Count}");
        sb.AppendLine($"- Recent arb opportunities: {snapshot.ArbOpportunities.Count}");
        sb.AppendLine($"- DeFi positions tracked: {snapshot.DefiHealth.Count}");

        if (snapshot.FailedSources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"⚠️  Unavailable data sources (may be offline): {string.Join(", ", snapshot.FailedSources)}");
        }

        sb.AppendLine();
        sb.AppendLine("Use your tools to retrieve live data before answering questions about positions,");
        sb.AppendLine("signals, strategies, or DeFi. Always confirm destructive or financial operations.");
        return sb.ToString();
    }
}
