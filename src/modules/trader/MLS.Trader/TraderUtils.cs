using System.Text.RegularExpressions;

namespace MLS.Trader;

/// <summary>
/// Shared sanitisation helpers used in log messages to prevent log-forging attacks.
/// </summary>
internal static partial class TraderUtils
{
    // Allow alphanumeric, hyphen, underscore, dot — covers clientOrderId, symbol, venueId formats.
    [GeneratedRegex(@"[^A-Za-z0-9\-_.]", RegexOptions.Compiled)]
    private static partial Regex SafeIdRegex();

    /// <summary>
    /// Sanitises any user-controlled identifier before embedding it in a log message.
    /// Strips all characters except alphanumerics, hyphen, underscore, and dot;
    /// truncates to 128 characters.
    /// </summary>
    internal static string SafeLog(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var truncated = value.Length > 128 ? value[..128] : value;
        return SafeIdRegex().Replace(truncated, "_");
    }
}
