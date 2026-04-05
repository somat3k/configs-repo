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
    /// </summary>
    /// <param name="query">Incoming query payload.</param>
    /// <param name="userId">Requesting user / client identifier — routes to their SignalR group.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessQueryAsync(AiQueryPayload query, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns an <see cref="IAsyncEnumerable{T}"/> of response chunks for SSE / HTTP streaming clients.
    /// Canvas actions are still dispatched as a side-effect.
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
        var sw = Stopwatch.StartNew();
        var provider = await SelectProviderAsync(query, userId, ct).ConfigureAwait(false);
        var modelId  = ResolveModelId(query, provider);
        var chatSvc  = provider.BuildService(modelId);
        var snapshot = await _context.AssembleAsync(userId, ct).ConfigureAwait(false);
        var history  = BuildChatHistory(query, snapshot);
        var kernel   = BuildKernel(chatSvc);

        int chunkIndex = 0;

        await foreach (var chunk in InnerStreamAsync(chatSvc, history, kernel, ct).ConfigureAwait(false))
        {
            var payload  = new AiResponseChunkPayload(chunkIndex++, chunk, false, 0);
            var envelope = EnvelopePayload.Create(MessageTypes.AiResponseChunk, ModuleId, payload);

            await _hub.Clients.Group(userId.ToString())
                .SendAsync("ReceiveEnvelope", envelope, ct)
                .ConfigureAwait(false);
        }

        sw.Stop();

        var completePayload  = new AiResponseCompletePayload(chunkIndex, sw.ElapsedMilliseconds, provider.ProviderId, modelId, 0);
        var completeEnvelope = EnvelopePayload.Create(MessageTypes.AiResponseComplete, ModuleId, completePayload);

        await _hub.Clients.Group(userId.ToString())
            .SendAsync("ReceiveEnvelope", completeEnvelope, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "ChatService: response complete — {Chunks} chunks, {Elapsed}ms, provider={Provider}, model={Model}",
            chunkIndex, sw.ElapsedMilliseconds, provider.ProviderId, modelId);
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

        int chunkIndex = 0;

        await foreach (var text in InnerStreamAsync(chatSvc, history, kernel, ct).ConfigureAwait(false))
        {
            yield return new AiResponseChunkPayload(chunkIndex++, text, false, 0);
        }

        // Terminal chunk signals end-of-stream to the SSE consumer
        yield return new AiResponseChunkPayload(chunkIndex, string.Empty, true, 0);
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

    private static string ResolveModelId(AiQueryPayload query, ILLMProvider provider) =>
        !string.IsNullOrWhiteSpace(query.ModelOverride)
            ? query.ModelOverride
            : provider.SupportedModels.Count > 0
                ? provider.SupportedModels[0]
                : throw new InvalidOperationException(
                    $"Provider '{provider.ProviderId}' reports no supported models.");

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

        // Replay prior turns from the conversation history
        foreach (var turn in query.ConversationHistory)
        {
            if (!turn.TryGetProperty("role", out var roleEl) ||
                !turn.TryGetProperty("content", out var contentEl))
                continue;

            var role    = roleEl.GetString();
            var content = contentEl.GetString() ?? string.Empty;

            if (role == "user")      history.AddUserMessage(content);
            else if (role == "assistant") history.AddAssistantMessage(content);
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
