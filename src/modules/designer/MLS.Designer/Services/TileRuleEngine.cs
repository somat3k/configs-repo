using System.Collections.Concurrent;
using System.Collections.Immutable;
using MLS.Core.Designer;

namespace MLS.Designer.Services;

/// <summary>
/// Evaluates the ordered <see cref="ITileRule"/> list of a <see cref="ICustomTile"/> against
/// each incoming <see cref="BlockSignal"/>, discovers socket references declared in rule
/// expressions, and executes the first matching rule's action.
/// </summary>
/// <remarks>
/// <para>Rules are evaluated top-to-bottom; the first matching condition fires its action
/// and processing stops (short-circuit).  If no rule matches, the signal is silently dropped.</para>
/// <para>Socket discovery parses simple <c>input[N]</c> / <c>output[N]</c> references from
/// DSL expressions so the canvas can derive the tile's socket topology without full execution.</para>
/// </remarks>
public sealed class TileRuleEngine
{
    // ── Socket discovery ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parse all input socket references (<c>input[N]</c>) declared in the given rules.
    /// Returns a sorted, de-duplicated list of socket names: <c>tile_input_0</c>, <c>tile_input_1</c>, …
    /// </summary>
    public IReadOnlyList<string> DiscoverInputSockets(IReadOnlyList<ITileRule> rules)
    {
        var indices = new SortedSet<int>();
        foreach (var rule in rules)
            CollectIndices(rule.Condition.Expression, "input", indices);
        return indices.Select(i => $"tile_input_{i}").ToList();
    }

    /// <summary>
    /// Parse all output socket references (<c>output[N]</c>) declared in the given rules.
    /// Returns a sorted, de-duplicated list of socket names: <c>tile_output_0</c>, <c>tile_output_1</c>, …
    /// </summary>
    public IReadOnlyList<string> DiscoverOutputSockets(IReadOnlyList<ITileRule> rules)
    {
        var indices = new SortedSet<int>();
        foreach (var rule in rules)
            CollectIndices(rule.Action.Expression, "output", indices);
        return indices.Select(i => $"tile_output_{i}").ToList();
    }

    // ── Evaluation ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluate the rule list against the incoming signal.
    /// Returns the first matching rule or <see langword="null"/> when nothing matches.
    /// </summary>
    public ITileRule? FindMatchingRule(IReadOnlyList<ITileRule> rules, BlockSignal signal)
    {
        foreach (var rule in rules)
        {
            if (rule.Condition.Evaluate(signal))
                return rule;
        }
        return null;
    }

    /// <summary>
    /// Execute the first matching rule against the incoming signal.
    /// Returns <see langword="true"/> when a rule was matched and its action executed;
    /// <see langword="false"/> when no rule matched (signal silently dropped).
    /// </summary>
    public async ValueTask<bool> ExecuteAsync(
        IReadOnlyList<ITileRule> rules,
        BlockSignal signal,
        ICustomTile tile,
        CancellationToken ct)
    {
        var match = FindMatchingRule(rules, signal);
        if (match is null)
            return false;

        await match.Action.ExecuteAsync(signal, tile, ct).ConfigureAwait(false);
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static void CollectIndices(string expression, string prefix, SortedSet<int> indices)
    {
        // Match patterns like: input[0], input[1], output[0] ...
        var searchToken = prefix + "[";
        var pos = 0;
        while (true)
        {
            var start = expression.IndexOf(searchToken, pos, StringComparison.Ordinal);
            if (start < 0) break;

            var openBracket = start + searchToken.Length - 1;
            var closeBracket = expression.IndexOf(']', openBracket + 1);
            if (closeBracket < 0) break;

            var indexStr = expression[(openBracket + 1)..closeBracket];
            if (int.TryParse(indexStr, out var idx))
                indices.Add(idx);

            pos = closeBracket + 1;
        }
    }
}
