using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MLS.DataLayer.Hydra;

/// <summary>
/// Shared utility helpers for the Hydra feed subsystem.
/// </summary>
internal static partial class HydraUtils
{
    // Allow only alphanumeric, hyphen, underscore, dot, and slash — covers all valid
    // exchange/symbol/timeframe identifiers.  Everything else is stripped.
    [GeneratedRegex(@"[^A-Za-z0-9\-_./]", RegexOptions.Compiled)]
    private static partial Regex SafeIdPattern();

    /// <summary>
    /// Sanitises a feed identifier (exchange, symbol, or timeframe) for safe use in
    /// log messages and external queries.  Strips any character that is not alphanumeric,
    /// hyphen, underscore, dot, or forward-slash.
    /// </summary>
    public static string SanitiseFeedId(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // Guard length to prevent log flooding
        var truncated = value.Length > 64 ? value[..64] : value;
        return SafeIdPattern().Replace(truncated, "_");
    }

    /// <summary>
    /// Derives the HYPERLIQUID coin name from an MLS symbol string.
    /// Strips a leading 'W' prefix (e.g. WBTC → BTC, WETH → ETH).
    /// </summary>
    public static string DeriveHyperliquidCoin(string symbol)
    {
        var base_ = symbol.Split('-', '/')[0].ToUpperInvariant();
        return base_.Length > 1 && base_[0] == 'W' ? base_[1..] : base_;
    }

    /// <summary>Maps MLS timeframe strings to HYPERLIQUID interval strings.</summary>
    public static string NormaliseHyperliquidInterval(string tf) => tf switch
    {
        "1m"  => "1m",  "3m"  => "3m",  "5m"  => "5m",
        "15m" => "15m", "30m" => "30m", "1h"  => "1h",
        "2h"  => "2h",  "4h"  => "4h",  "8h"  => "8h",
        "1d"  => "1d",  "1w"  => "1w",  _     => tf
    };

    /// <summary>Converts a timeframe string to total seconds.</summary>
    public static double TimeframeToSeconds(string tf) => tf switch
    {
        "1m"  => 60,
        "3m"  => 180,
        "5m"  => 300,
        "15m" => 900,
        "30m" => 1_800,
        "1h"  => 3_600,
        "2h"  => 7_200,
        "4h"  => 14_400,
        "8h"  => 28_800,
        "1d"  => 86_400,
        "1w"  => 604_800,
        _     => 60,
    };

    /// <summary>Converts a timeframe string to a <see cref="TimeSpan"/> poll interval.</summary>
    public static TimeSpan TimeframeToInterval(string tf) =>
        TimeSpan.FromSeconds(TimeframeToSeconds(tf));

    /// <summary>
    /// Sanitises a peer ID (moduleId / clientId) for use as a SignalR group name.
    /// Truncates to 64 characters and replaces carriage-return / line-feed with underscores.
    /// </summary>
    public static string SanitisePeerId(string id) =>
        (id.Length > 64 ? id[..64] : id)
            .Replace('\r', '_')
            .Replace('\n', '_');

    /// <summary>
    /// Parses a numeric value from a <see cref="JsonElement"/> that may be either a JSON
    /// <c>number</c> or a JSON <c>string</c> representation of a number (e.g. <c>"65000.5"</c>).
    /// Returns 0.0 when the element is absent or cannot be parsed.
    /// </summary>
    public static double ParseJsonDouble(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number => v.GetDouble(),
        JsonValueKind.String when double.TryParse(
            v.GetString(),
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var r) => r,
        _ => 0.0,
    };

    /// <summary>
    /// Tries to get the double value of property <paramref name="key"/> from a JSON object element.
    /// Returns 0.0 when the property is absent or unparsable.
    /// </summary>
    public static double GetJsonDouble(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) ? ParseJsonDouble(v) : 0.0;
}
