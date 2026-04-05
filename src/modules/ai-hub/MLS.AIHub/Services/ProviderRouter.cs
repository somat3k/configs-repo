using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MLS.AIHub.Configuration;
using MLS.AIHub.Persistence;
using MLS.AIHub.Providers;
using MLS.Core.Contracts.Designer;

namespace MLS.AIHub.Services;

/// <summary>
/// User-defined distributor that selects the active <see cref="ILLMProvider"/> for each request.
/// </summary>
/// <remarks>
/// Selection priority:
/// <list type="number">
///   <item>Per-request provider override (<c>AI_QUERY.provider_override</c>) — probed for availability before use.</item>
///   <item>User preference primary provider (persisted in PostgreSQL via <see cref="IUserPreferenceRepository"/>).</item>
///   <item>Configured fallback chain from <see cref="AIHubOptions.FallbackChain"/> — each candidate is probed for availability in order.</item>
///   <item>Local provider (Ollama) — always available as the final fallback.</item>
/// </list>
/// Circuit Breaker: providers with 3+ consecutive failures are skipped for 60 seconds.
/// </remarks>
public sealed class ProviderRouter(
    IEnumerable<ILLMProvider> _providers,
    IUserPreferenceRepository _prefs,
    IOptions<AIHubOptions> _options,
    ILogger<ProviderRouter> _logger) : IProviderRouter
{
    // Built once and cached for the lifetime of this (scoped) instance.
    private readonly IReadOnlyDictionary<string, ILLMProvider> _providerMap =
        _providers.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<ILLMProvider> SelectProviderAsync(
        AiQueryPayload query, Guid userId, CancellationToken ct = default)
    {
        // 1. Per-request override — probe before committing to the override
        if (!string.IsNullOrWhiteSpace(query.ProviderOverride) &&
            _providerMap.TryGetValue(query.ProviderOverride, out var overrideProvider))
        {
            if (!overrideProvider.IsAvailable)
            {
                _logger.LogInformation(
                    "Per-request provider override {ProviderId} is circuit-open — continuing with standard selection",
                    overrideProvider.ProviderId);
            }
            else
            {
                var overrideAvailable = await overrideProvider.CheckAvailabilityAsync(ct).ConfigureAwait(false);
                if (overrideAvailable)
                {
                    _logger.LogDebug("Using per-request provider override: {ProviderId}", overrideProvider.ProviderId);
                    return overrideProvider;
                }

                _logger.LogInformation(
                    "Per-request provider override {ProviderId} failed availability probe — continuing with standard selection",
                    overrideProvider.ProviderId);
            }
        }

        // 2. User preference (loaded from PostgreSQL)
        var pref = await _prefs.GetAsync(userId, ct).ConfigureAwait(false);
        if (pref is not null &&
            _providerMap.TryGetValue(pref.PrimaryProviderId, out var preferredProvider) &&
            preferredProvider.IsAvailable)
        {
            var available = await preferredProvider.CheckAvailabilityAsync(ct).ConfigureAwait(false);
            if (available)
            {
                _logger.LogDebug("Using user preferred provider: {ProviderId}", preferredProvider.ProviderId);
                return preferredProvider;
            }

            _logger.LogInformation(
                "User preferred provider {ProviderId} unavailable — walking fallback chain",
                preferredProvider.ProviderId);
        }

        // 3. Walk fallback chain — user preference chain first, then global configured chain
        var fallbackChain = pref?.FallbackChain ?? _options.Value.FallbackChain;

        foreach (var providerId in fallbackChain)
        {
            if (!_providerMap.TryGetValue(providerId, out var candidate))
                continue;

            if (!candidate.IsAvailable)
            {
                _logger.LogDebug("Skipping circuit-open provider: {ProviderId}", candidate.ProviderId);
                continue;
            }

            var available = await candidate.CheckAvailabilityAsync(ct).ConfigureAwait(false);
            if (available)
            {
                _logger.LogInformation("Fallback provider selected: {ProviderId}", candidate.ProviderId);
                return candidate;
            }
        }

        // 4. Final fallback: Local (always available)
        if (_providerMap.TryGetValue("local", out var local))
        {
            _logger.LogWarning("All remote providers unavailable — falling back to Local provider");
            return local;
        }

        throw new InvalidOperationException(
            "No LLM provider is available. Ensure at least one provider (preferably Local) is registered.");
    }
}
