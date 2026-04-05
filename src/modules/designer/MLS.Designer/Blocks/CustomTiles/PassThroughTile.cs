using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.CustomTiles;

/// <summary>
/// Identity tile — forwards every incoming signal to its output unchanged.
/// The simplest possible <see cref="ICustomTile"/>: a single <c>ALWAYS → PASS_THROUGH</c> rule
/// with no computation, used as a cable organiser or fan-out junction on the canvas.
/// </summary>
/// <remarks>
/// <para>Input:  <see cref="BlockSocketType.IndicatorValue"/> on socket <c>passthrough_input</c>.</para>
/// <para>Output: <see cref="BlockSocketType.IndicatorValue"/> on socket <c>passthrough_output</c>.</para>
/// <para>
/// Adding rules to a <see cref="PassThroughTile"/> via <see cref="AddRule"/> is intentionally
/// a no-op: the tile is designed to be a pure pass-through and does not participate in the
/// rule evaluation engine.  Use <see cref="CustomIndicatorTile"/> for rule-based logic.
/// </para>
/// </remarks>
public sealed class PassThroughTile : BlockBase, ICustomTile
{
    private static readonly ITileRule[] _defaultRules =
    [
        new TileRule(new AlwaysCondition(), new PassThroughAction("PASS_THROUGH output[0]")),
    ];

    /// <inheritdoc/>
    public override string BlockType   => "PassThroughTile";
    /// <inheritdoc/>
    public override string DisplayName => TileName;
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters { get; } = [];

    // ── ICustomTile ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string TileName { get; set; } = "PassThrough";

    /// <inheritdoc/>
    public IReadOnlyList<ITileRule> Rules => _defaultRules;

    /// <summary>Initialises a <see cref="PassThroughTile"/>.</summary>
    public PassThroughTile() : base(
        [BlockSocket.Input("passthrough_input",   BlockSocketType.IndicatorValue)],
        [BlockSocket.Output("passthrough_output", BlockSocketType.IndicatorValue)]) { }

    /// <inheritdoc/>
    public void AddRule(ITileRule rule) { /* no-op: pass-through tiles don't support rule customisation */ }

    /// <inheritdoc/>
    public void RemoveRule(int index) { /* no-op */ }

    /// <inheritdoc/>
    public void MoveRule(int fromIndex, int toIndex) { /* no-op */ }

    // ── BlockBase ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        // Forward the signal directly; return it so BlockBase emits via OutputProduced
        var forwarded = signal with
        {
            SourceSocketName = "passthrough_output",
        };
        return new ValueTask<BlockSignal?>(forwarded);
    }
}
