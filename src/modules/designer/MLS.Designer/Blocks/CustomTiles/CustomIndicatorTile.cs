using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;
using MLS.Designer.Services;

namespace MLS.Designer.Blocks.CustomTiles;

// ── Concrete tile condition implementations ───────────────────────────────────

/// <summary>
/// Condition that always evaluates to <see langword="true"/>.  Used as the final
/// catch-all / default rule in a tile rule sequence.
/// </summary>
public sealed class AlwaysCondition : ITileCondition
{
    /// <inheritdoc/>
    public string Expression => "ALWAYS";

    /// <inheritdoc/>
    public bool Evaluate(BlockSignal signal) => true;
}

/// <summary>
/// Numeric threshold condition: evaluates <c>input[N].value OP threshold</c>.
/// </summary>
public sealed class NumericThresholdCondition(string expression, Func<double, double, bool> comparator, double threshold) : ITileCondition
{
    /// <inheritdoc/>
    public string Expression { get; } = expression;

    /// <inheritdoc/>
    public bool Evaluate(BlockSignal signal)
    {
        if (signal.Value.ValueKind == JsonValueKind.Number)
            return comparator(signal.Value.GetDouble(), threshold);

        if (signal.Value.ValueKind == JsonValueKind.Object &&
            signal.Value.TryGetProperty("value", out var val) &&
            val.TryGetDouble(out var d))
            return comparator(d, threshold);

        return false;
    }
}

// ── Concrete tile action implementations ─────────────────────────────────────

/// <summary>
/// Action that forwards the incoming signal to the owning tile's <c>OutputProduced</c>
/// event unchanged (pass-through).
/// </summary>
public sealed class PassThroughAction(string expression) : ITileAction
{
    /// <inheritdoc/>
    public string Expression { get; } = expression;

    /// <inheritdoc/>
    public ValueTask ExecuteAsync(BlockSignal signal, ICustomTile tile, CancellationToken ct)
    {
        if (tile is CustomIndicatorTile concrete)
            return concrete.EmitSignalInternalAsync(signal, ct);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Action that applies a scalar multiplier to the signal value and emits the result.
/// </summary>
public sealed class ComputeMultiplyAction(string expression, double multiplier) : ITileAction
{
    /// <inheritdoc/>
    public string Expression { get; } = expression;

    /// <inheritdoc/>
    public ValueTask ExecuteAsync(BlockSignal signal, ICustomTile tile, CancellationToken ct)
    {
        double inputValue = 0;
        if (signal.Value.ValueKind == JsonValueKind.Number)
            inputValue = signal.Value.GetDouble();
        else if (signal.Value.ValueKind == JsonValueKind.Object &&
                 signal.Value.TryGetProperty("value", out var v))
            v.TryGetDouble(out inputValue);

        var result = inputValue * multiplier;
        var computed = new BlockSignal(
            signal.SourceBlockId,
            signal.SourceSocketName,
            signal.SocketType,
            JsonSerializer.SerializeToElement(result));

        if (tile is CustomIndicatorTile concrete)
            return concrete.EmitSignalInternalAsync(computed, ct);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Action that halts processing — no output is emitted and the signal is discarded.
/// </summary>
public sealed class HaltAction : ITileAction
{
    /// <inheritdoc/>
    public string Expression => "HALT";

    /// <inheritdoc/>
    public ValueTask ExecuteAsync(BlockSignal signal, ICustomTile tile, CancellationToken ct) =>
        ValueTask.CompletedTask;
}

// ── Concrete tile rule ────────────────────────────────────────────────────────

/// <summary>
/// Default implementation of <see cref="ITileRule"/> that pairs a condition with an action.
/// </summary>
public sealed class TileRule(ITileCondition condition, ITileAction action) : ITileRule
{
    /// <inheritdoc/>
    public Guid RuleId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public ITileCondition Condition { get; } = condition;

    /// <inheritdoc/>
    public ITileAction Action { get; } = action;

    /// <inheritdoc/>
    public string DslSource => $"{Condition.Expression} → {Action.Expression}";
}

// ── CustomIndicatorTile ───────────────────────────────────────────────────────

/// <summary>
/// Concrete implementation of <see cref="ICustomTile"/>.
/// Hosts an ordered list of <see cref="ITileRule"/> objects and evaluates them against
/// each incoming <see cref="BlockSignal"/> using <see cref="TileRuleEngine"/>.
/// Socket topology is re-derived from the rule set whenever a rule is added or removed.
/// </summary>
/// <remarks>
/// A default pass-through rule is added on construction so the tile is immediately usable
/// as a transparent relay.  Users can replace or extend the rule set via the canvas UI.
/// </remarks>
public sealed class CustomIndicatorTile : BlockBase, ICustomTile
{
    private readonly TileRuleEngine _engine = new();
    private readonly List<ITileRule> _rules = [];

    // Dynamic socket lists rebuilt on every rule change
    private List<IBlockSocket> _dynamicInputs;
    private List<IBlockSocket> _dynamicOutputs;

    /// <inheritdoc/>
    public override string BlockType   => "CustomIndicatorTile";
    /// <inheritdoc/>
    public override string DisplayName => TileName;
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters { get; } = [];
    /// <inheritdoc/>
    public override IReadOnlyList<IBlockSocket> InputSockets  => _dynamicInputs;
    /// <inheritdoc/>
    public override IReadOnlyList<IBlockSocket> OutputSockets => _dynamicOutputs;

    // ── ICustomTile ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string TileName { get; set; } = "CustomTile";

    /// <inheritdoc/>
    public IReadOnlyList<ITileRule> Rules => _rules;

    /// <summary>Initialises a <see cref="CustomIndicatorTile"/> with a default pass-through rule.</summary>
    public CustomIndicatorTile() : base([], [])
    {
        _dynamicInputs  = [BlockSocket.Input("tile_input_0",  BlockSocketType.IndicatorValue)];
        _dynamicOutputs = [BlockSocket.Output("tile_output_0", BlockSocketType.IndicatorValue)];

        // Default rule: always pass through
        _rules.Add(new TileRule(new AlwaysCondition(), new PassThroughAction("PASS_THROUGH output[0]")));
    }

    /// <inheritdoc/>
    public void AddRule(ITileRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        RebuildSockets();
    }

    /// <inheritdoc/>
    public void RemoveRule(int index)
    {
        if (index < 0 || index >= _rules.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _rules.RemoveAt(index);
        RebuildSockets();
    }

    /// <inheritdoc/>
    public void MoveRule(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _rules.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= _rules.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));
        if (fromIndex == toIndex) return;

        var rule = _rules[fromIndex];
        _rules.RemoveAt(fromIndex);
        _rules.Insert(toIndex, rule);
        // Socket topology does not change on reorder — no socket rebuild required
    }

    // ── BlockBase ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override async ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        // Evaluation and emission is handled by the rule engine + EmitSignalInternalAsync.
        // ProcessCoreAsync returns null so BlockBase does NOT double-emit via OutputProduced.
        await _engine.ExecuteAsync(_rules, signal, this, ct).ConfigureAwait(false);
        return null;
    }

    // ── Internal emission helper (called by action implementations) ───────────────

    /// <summary>Internal helper called by action objects to route output through OutputProduced.</summary>
    internal ValueTask EmitSignalInternalAsync(BlockSignal signal, CancellationToken ct) =>
        EmitSignalAsync(signal, ct);

    // ── Private helpers ───────────────────────────────────────────────────────────

    private void RebuildSockets()
    {
        var inputNames  = _engine.DiscoverInputSockets(_rules);
        var outputNames = _engine.DiscoverOutputSockets(_rules);

        _dynamicInputs  = inputNames.Count > 0
            ? inputNames.Select(n  => (IBlockSocket)BlockSocket.Input(n,  BlockSocketType.IndicatorValue)).ToList()
            : [BlockSocket.Input("tile_input_0", BlockSocketType.IndicatorValue)];

        _dynamicOutputs = outputNames.Count > 0
            ? outputNames.Select(n => (IBlockSocket)BlockSocket.Output(n, BlockSocketType.IndicatorValue)).ToList()
            : [BlockSocket.Output("tile_output_0", BlockSocketType.IndicatorValue)];
    }
}
