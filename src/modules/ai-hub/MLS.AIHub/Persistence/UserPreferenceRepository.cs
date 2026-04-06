using Microsoft.EntityFrameworkCore;

namespace MLS.AIHub.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUserPreferenceRepository"/>.
/// Reads and persists per-user LLM provider selections in PostgreSQL.
/// </summary>
public sealed class UserPreferenceRepository(AIHubDbContext _db) : IUserPreferenceRepository
{
    /// <inheritdoc/>
    public async Task<UserPreference?> GetAsync(Guid userId, CancellationToken ct = default)
        => await _db.UserPreferences.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId, ct)
                    .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task SaveAsync(UserPreference preference, CancellationToken ct = default)
    {
        preference.UpdatedAt = DateTimeOffset.UtcNow;

        var existing = await _db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == preference.UserId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.UserPreferences.Add(preference);
        }
        else
        {
            existing.PrimaryProviderId  = preference.PrimaryProviderId;
            existing.PreferredModelId   = preference.PreferredModelId;
            existing.FallbackChainRaw   = preference.FallbackChainRaw;
            existing.ProviderConfigsJson = preference.ProviderConfigsJson;
            existing.UpdatedAt          = preference.UpdatedAt;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
