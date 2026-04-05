namespace MLS.Core.Designer;

/// <summary>
/// A user-composable tile element that exposes dynamic typed sockets and an editable
/// rule sequence.  Tiles can be connected to any compatible block socket on the Designer canvas.
/// </summary>
/// <remarks>
/// Unlike fixed indicator blocks, a <see cref="ICustomTile"/> derives its socket topology
/// dynamically from the rules it contains.  Adding or removing a rule triggers socket
/// re-discovery so the canvas connection UI always reflects the current topology.
/// </remarks>
public interface ICustomTile : IBlockElement
{
    /// <summary>User-assigned display name for this tile on the canvas.</summary>
    string TileName { get; set; }

    /// <summary>
    /// Ordered sequence of rules evaluated against each incoming signal.
    /// Rules are evaluated top-to-bottom; the first matching rule's action is applied.
    /// </summary>
    IReadOnlyList<ITileRule> Rules { get; }

    /// <summary>Add a rule to the evaluation sequence and re-derive socket topology.</summary>
    void AddRule(ITileRule rule);

    /// <summary>Remove a rule by its zero-based index and re-derive socket topology.</summary>
    void RemoveRule(int index);

    /// <summary>Reorder rules (supports drag-and-drop from the UI).</summary>
    void MoveRule(int fromIndex, int toIndex);
}

/// <summary>
/// A single if/then rule inside a <see cref="ICustomTile"/>.
/// </summary>
public interface ITileRule
{
    /// <summary>Unique identifier for this rule, used for serialisation round-trips.</summary>
    Guid RuleId { get; }

    /// <summary>
    /// Condition evaluated against the incoming signal payload.
    /// Examples: <c>"input[0].value &gt; threshold"</c>, <c>"input[1].symbol == filter"</c>, <c>"ALWAYS"</c>.
    /// </summary>
    ITileCondition Condition { get; }

    /// <summary>
    /// Action applied when the condition is <see langword="true"/>.
    /// Examples: emit on output[0], apply arithmetic, pass-through, store, halt.
    /// </summary>
    ITileAction Action { get; }

    /// <summary>
    /// DSL source string for this rule, stored alongside the strategy graph schema for serialisation.
    /// Format: <c>"{condition} → {action}"</c>.
    /// </summary>
    string DslSource { get; }
}

/// <summary>Condition that can be evaluated against a <see cref="BlockSignal"/> payload.</summary>
public interface ITileCondition
{
    /// <summary>
    /// Evaluate the condition against the incoming signal value.
    /// Returns <see langword="true"/> when the rule's if-branch should fire.
    /// </summary>
    bool Evaluate(BlockSignal signal);

    /// <summary>Serialisable DSL expression for this condition, e.g. <c>"input[0].value &gt; 2.5"</c>.</summary>
    string Expression { get; }
}

/// <summary>Action executed when a rule's condition is satisfied.</summary>
public interface ITileAction
{
    /// <summary>
    /// Execute the action on the incoming signal.
    /// Implementations may emit zero or more output signals or transform the payload in-place.
    /// </summary>
    /// <param name="signal">Incoming signal that triggered the rule.</param>
    /// <param name="tile">The owning tile (provides access to internal state and output sockets).</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask ExecuteAsync(BlockSignal signal, ICustomTile tile, CancellationToken ct);

    /// <summary>Serialisable DSL expression for this action, e.g. <c>"PASS_THROUGH output[0]"</c>.</summary>
    string Expression { get; }
}
