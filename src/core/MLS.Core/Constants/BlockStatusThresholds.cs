namespace MLS.Core.Constants;

/// <summary>
/// Threshold constants used to derive block operational status on the designer canvas.
/// </summary>
public static class BlockStatusThresholds
{
    /// <summary>
    /// Error-rate percentage (over the last 60 seconds) at or above which a block
    /// transitions to <c>BlockStatus.Error</c>.
    /// </summary>
    public const double ErrorRatePct = 20.0;

    /// <summary>
    /// Error-rate percentage (over the last 60 seconds) at or above which a block
    /// transitions to <c>BlockStatus.Degraded</c>.
    /// </summary>
    public const double DegradedRatePct = 5.0;
}
