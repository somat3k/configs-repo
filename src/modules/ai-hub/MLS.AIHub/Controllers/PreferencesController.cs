using Microsoft.AspNetCore.Mvc;
using MLS.AIHub.Persistence;

namespace MLS.AIHub.Controllers;

/// <summary>
/// REST endpoints for per-user LLM provider preference management.
/// Preferences include primary provider, preferred model, fallback chain,
/// and per-provider configuration (model IDs + endpoints; not API keys).
/// </summary>
[ApiController]
[Route("api/preferences")]
public sealed class PreferencesController(
    IUserPreferenceRepository _repo,
    ILogger<PreferencesController> _logger) : ControllerBase
{
    // ── GET /api/preferences/{userId} ─────────────────────────────────────────

    /// <summary>Returns the saved preferences for <paramref name="userId"/>.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with preference data, or 404 when no record exists.</returns>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid userId, CancellationToken ct)
    {
        var pref = await _repo.GetAsync(userId, ct).ConfigureAwait(false);
        if (pref is null)
            return NotFound(new { error = $"No preferences found for userId {userId}." });

        return Ok(new
        {
            user_id               = pref.UserId,
            primary_provider_id   = pref.PrimaryProviderId,
            preferred_model_id    = pref.PreferredModelId,
            fallback_chain        = pref.FallbackChain,
            provider_configs_json = pref.ProviderConfigsJson,
            updated_at            = pref.UpdatedAt,
        });
    }

    // ── POST /api/preferences ─────────────────────────────────────────────────

    /// <summary>Creates or updates user LLM provider preferences.</summary>
    /// <param name="request">Preference payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 when saved successfully.</returns>
    [HttpPost("")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveAsync(
        [FromBody] SavePreferencesRequest request,
        CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return BadRequest(new { error = "UserId must not be empty." });

        if (string.IsNullOrWhiteSpace(request.PrimaryProviderId))
            return BadRequest(new { error = "PrimaryProviderId is required." });

        var preference = new UserPreference
        {
            UserId               = request.UserId,
            PrimaryProviderId    = request.PrimaryProviderId,
            PreferredModelId     = request.PreferredModelId ?? "",
            FallbackChainRaw     = string.Join(',', request.FallbackChain ?? []),
            ProviderConfigsJson  = request.ProviderConfigsJson ?? "{}",
        };

        await _repo.SaveAsync(preference, ct).ConfigureAwait(false);

        // Sanitize user-provided value before logging to prevent log injection.
        var safePrimary = request.PrimaryProviderId
            .Replace('\n', '_').Replace('\r', '_');
        _logger.LogInformation(
            "Saved preferences for userId {UserId}, primary={Primary}",
            request.UserId, safePrimary);

        return Ok(new { saved = true, user_id = request.UserId });
    }
}

// ── Request DTO ───────────────────────────────────────────────────────────────

/// <summary>Body for <c>POST /api/preferences</c>.</summary>
/// <param name="UserId">User identifier.</param>
/// <param name="PrimaryProviderId">Primary LLM provider (e.g. <c>"openai"</c>).</param>
/// <param name="PreferredModelId">Preferred model within the primary provider.</param>
/// <param name="FallbackChain">Ordered list of fallback provider IDs.</param>
/// <param name="ProviderConfigsJson">
/// JSON object mapping provider IDs to their model/endpoint settings.
/// API keys are NOT included (must be set via environment variables).
/// </param>
public sealed record SavePreferencesRequest(
    Guid UserId,
    string PrimaryProviderId,
    string? PreferredModelId,
    string[]? FallbackChain,
    string? ProviderConfigsJson);
