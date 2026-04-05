namespace MLS.AIHub.Persistence;

/// <summary>Repository for per-user AI Hub preferences.</summary>
public interface IUserPreferenceRepository
{
    /// <summary>Gets the preference record for <paramref name="userId"/>, or <see langword="null"/> if not set.</summary>
    Task<UserPreference?> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Persists or updates the preference record for the user identified in <paramref name="preference"/>.</summary>
    Task SaveAsync(UserPreference preference, CancellationToken ct = default);
}
