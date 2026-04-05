using MLS.AIHub.Providers;
using MLS.Core.Contracts.Designer;

namespace MLS.AIHub.Services;

/// <summary>
/// Selects the appropriate <see cref="ILLMProvider"/> for an incoming AI query.
/// </summary>
public interface IProviderRouter
{
    /// <summary>
    /// Selects the best available provider for <paramref name="query"/> on behalf of
    /// <paramref name="userId"/>. Applies per-request override → user preference → fallback chain → Local.
    /// </summary>
    Task<ILLMProvider> SelectProviderAsync(AiQueryPayload query, Guid userId, CancellationToken ct = default);
}
