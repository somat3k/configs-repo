using Microsoft.SemanticKernel.ChatCompletion;

namespace MLS.AIHub.Providers;

/// <summary>
/// Wraps a Semantic Kernel <see cref="IChatCompletionService"/> for a specific LLM provider.
/// Each concrete provider registers the models it supports and builds the SK service on demand.
/// In SK 1.74+, <see cref="IChatCompletionService"/> covers both batch and streaming completions.
/// </summary>
public interface ILLMProvider
{
    /// <summary>Unique identifier for this provider (e.g. <c>"openai"</c>, <c>"anthropic"</c>).</summary>
    string ProviderId { get; }

    /// <summary>Human-readable display name.</summary>
    string DisplayName { get; }

    /// <summary>Ordered list of model IDs supported by this provider.</summary>
    IReadOnlyList<string> SupportedModels { get; }

    /// <summary>Whether this provider is currently considered available (not circuit-broken).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Performs a live availability probe. Returns <see langword="true"/> when the provider
    /// API is reachable. Must complete within 500 ms.
    /// </summary>
    Task<bool> CheckAvailabilityAsync(CancellationToken ct = default);

    /// <summary>
    /// Builds and returns a <see cref="IChatCompletionService"/> for the given model.
    /// The returned service supports both batch and streaming completions (SK 1.74+).
    /// </summary>
    /// <param name="modelId">Model to use; must be present in <see cref="SupportedModels"/>.</param>
    IChatCompletionService BuildService(string modelId);
}
