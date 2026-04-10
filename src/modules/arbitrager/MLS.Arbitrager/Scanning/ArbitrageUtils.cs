using System.Text.RegularExpressions;

namespace MLS.Arbitrager.Scanning;

/// <summary>
/// Sanitisation helpers for arbitrage identifiers used in log messages and queries.
/// </summary>
internal static partial class ArbitrageUtils
{
    // Allow only alphanumeric, hyphen, underscore, dot, and forward-slash.
    [GeneratedRegex(@"[^A-Za-z0-9\-_./]", RegexOptions.Compiled)]
    private static partial Regex SafeIdPattern();

    /// <summary>
    /// Sanitises a user-supplied identifier (exchange, symbol, etc.) for safe use in log messages.
    /// Strips characters that are not alphanumeric, hyphen, underscore, dot, or slash.
    /// Truncates to 64 characters to prevent log flooding.
    /// </summary>
    public static string SanitiseId(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var truncated = value.Length > 64 ? value[..64] : value;
        return SafeIdPattern().Replace(truncated, "_");
    }
}
