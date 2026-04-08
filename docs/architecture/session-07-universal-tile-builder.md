> ✅ **Status: Complete** — Implemented and verified in session 23 (workflow-demo).

# Session 07 — Universal Tile Builder: `ICustomTile` Abstraction Layer

> **Reference**: [Designer Block Graph](designer-block-graph.md) | [Session Schedule](../session-schedule.md) (Session 07)
> **Skills**: `.skills/designer.md` · `.skills/dotnet-devs.md` · `.skills/web-apps.md`

---

## Overview

The `FeatureEngineerBlock` (Session 06) computes a **fixed set** of market indicators
(RSI, MACD, Bollinger Bands, ATR, VWAP, VolDelta, SpreadBps, VwapDist).  This works
well as a default pipeline but does not allow users to define their own indicators,
filtering rules, or if/then signal logic on the canvas.

Session 07 introduces the **Universal Tile Builder** — a composable abstraction layer
that lets any user assemble professional indicators and rule-enforcer pipelines from
re-usable tiles (nodes) on the Designer canvas, without writing low-level code.

---

## Architectural Vision

```
┌──────────────────────────────────────────────────────────────────┐
│  CustomIndicatorTile  (ICustomTile)                               │
│                                                                    │
│  ┌─────────────┐   ┌──────────────┐   ┌───────────────────────┐  │
│  │  Input[0]   │──▶│  Rule Engine │──▶│  Output[0..n]         │  │
│  │  (stream)   │   │  (if/then)   │   │  (computed / pass-thru│  │
│  └─────────────┘   └──────────────┘   └───────────────────────┘  │
│                                                                    │
│  Parameters: { symbol, exchange, key, type, threshold, action }   │
└──────────────────────────────────────────────────────────────────┘
```

### Core Interfaces

```csharp
namespace MLS.Core.Designer;

/// <summary>
/// A user-composable tile element that exposes dynamic typed sockets and
/// an editable rule sequence.  Tiles can be connected to any compatible
/// block socket on the Designer canvas.
/// </summary>
public interface ICustomTile : IBlockElement
{
    /// <summary>User-assigned display name for this tile on the canvas.</summary>
    string TileName { get; set; }

    /// <summary>
    /// Ordered sequence of rules evaluated against each incoming signal.
    /// Rules are evaluated top-to-bottom; the first matching rule's action
    /// is applied.
    /// </summary>
    IReadOnlyList<ITileRule> Rules { get; }

    /// <summary>Add a rule to the evaluation sequence.</summary>
    void AddRule(ITileRule rule);

    /// <summary>Remove a rule by its index.</summary>
    void RemoveRule(int index);

    /// <summary>Reorder rules (drag-and-drop from the UI).</summary>
    void MoveRule(int fromIndex, int toIndex);
}

/// <summary>
/// A single if/then rule inside a CustomIndicatorTile.
/// </summary>
public interface ITileRule
{
    Guid RuleId { get; }

    /// <summary>
    /// Condition: evaluated against the signal payload.
    /// E.g. "input[0].value > threshold" or "input[1].symbol == filter".
    /// </summary>
    ITileCondition Condition { get; }

    /// <summary>
    /// Action applied when the condition is true.
    /// E.g. emit on output[0], apply computation, pass-through, store.
    /// </summary>
    ITileAction Action { get; }
}

/// <summary>Condition that can be evaluated against a BlockSignal payload.</summary>
public interface ITileCondition
{
    /// <summary>
    /// Evaluate the condition against the incoming signal value.
    /// Returns true when the rule's if-branch should fire.
    /// </summary>
    bool Evaluate(BlockSignal signal);
}

/// <summary>Action executed when a rule's condition is satisfied.</summary>
public interface ITileAction
{
    /// <summary>
    /// Execute the action on the incoming signal.
    /// May emit one or more output signals or transform the payload in-place.
    /// </summary>
    ValueTask ExecuteAsync(BlockSignal signal, ICustomTile tile, CancellationToken ct);
}
```

---

## Tile Rule DSL (User-Facing)

Rules are authored in a lightweight tile DSL that is serialised to JSON and stored
alongside the strategy graph.  The DSL is intentionally terse so that non-programmers
can compose rules from the UI drop-downs.

### Condition Syntax

| Expression | Meaning |
|------------|---------|
| `input[N].value > X` | Value on input socket N exceeds threshold X |
| `input[N].value < X` | Value on input socket N is below threshold X |
| `input[N].symbol == "SYM"` | Symbol field on input socket N matches string |
| `input[N].exchange == "EX"` | Exchange field matches |
| `input[N].type == BlockSocketType.Price` | Socket data type matches enum |
| `input[N].key contains "K"` | Payload key string contains substring |
| `ALWAYS` | Unconditional (default / pass-through rule) |

### Action Syntax

| Action | Meaning |
|--------|---------|
| `PASS_THROUGH output[M]` | Forward input signal to output socket M unchanged |
| `COMPUTE score[N] = expr` | Apply arithmetic expression; emit result on output N |
| `EMIT output[M] value=V` | Emit constant or computed value V on output M |
| `STORE key=K value=V` | Write value to tile's internal state dictionary |
| `HALT` | Stop processing (no output emitted) |

### Example Rule Sequence

```json
{
  "tile_name": "VolatilityFilter",
  "rules": [
    {
      "condition": "input[0].value > 2.5",
      "action": "COMPUTE score[0] = input[0].value * 1.5; EMIT output[0] value=score[0]"
    },
    {
      "condition": "input[0].value < 0.5",
      "action": "HALT"
    },
    {
      "condition": "ALWAYS",
      "action": "PASS_THROUGH output[0]"
    }
  ]
}
```

---

## Directory Scaffolding

The following files should be created in Session 07:

```
src/
  core/
    MLS.Core/
      Designer/
        ICustomTile.cs               ← New: tile interface
        ITileRule.cs                 ← New: rule + condition + action interfaces
        ITileCondition.cs            ← New: condition evaluator contract
        ITileAction.cs               ← New: action executor contract
        TileRuleDsl.cs               ← New: DSL parser (condition → ITileCondition)

  modules/
    designer/
      MLS.Designer/
        Blocks/
          CustomTiles/
            CustomIndicatorTile.cs   ← New: concrete ICustomTile + rule engine
            PassThroughTile.cs       ← New: identity tile (PASS_THROUGH only)
            ComputeTile.cs           ← New: arithmetic expression tile

        Services/
          TileRuleEngine.cs          ← New: evaluates ITileRule list per signal
          TileRuleEngineTests.cs     ← New: unit tests for rule evaluation

docs/
  architecture/
    session-07-universal-tile-builder.md   ← This file
```

---

## Socket Dynamic Registration

Unlike the fixed `FeatureEngineerBlock` which has a hard-coded set of input/output
sockets, a `CustomIndicatorTile` registers its sockets **dynamically** based on the
rule sequence:

```csharp
// Automatic socket discovery on rule add:
public void AddRule(ITileRule rule)
{
    _rules.Add(rule);
    // Re-derive input and output socket sets from all rule expressions
    _inputSockets  = _ruleEngine.DiscoverInputSockets(_rules);
    _outputSockets = _ruleEngine.DiscoverOutputSockets(_rules);
}
```

The `BlockRegistry` re-introspects each tile after every rule edit so the canvas
connection UI reflects the current socket topology.

---

## Integration with Session 06 FeatureEngineerBlock

The existing `FeatureEngineerBlock` is **not replaced** — it remains the default
fast-path for standard market indicators.  `CustomIndicatorTile` is a **parallel
extension** for user-defined logic.  Both register with the same `IBlockRegistry`
and can be connected to the same downstream `TrainSplitBlock` or `DataLoaderBlock`.

```
CandleFeedBlock
    │
    ├──▶ FeatureEngineerBlock (default: RSI/MACD/BB/ATR/VWAP)
    │
    └──▶ CustomIndicatorTile (user rules)
              │
              └──▶ TrainSplitBlock
```

---

## Acceptance Criteria (Session 07)

- [ ] `ICustomTile`, `ITileRule`, `ITileCondition`, `ITileAction` compile in `MLS.Core`
- [ ] `CustomIndicatorTile.AddRule` updates socket topology; `BlockRegistry` reflects change
- [ ] `TileRuleEngine` evaluates condition → action per signal; `HALT` stops propagation
- [ ] DSL parser round-trips: `string → ITileRule → JSON → ITileRule`
- [ ] Unit tests: `TileRuleEngineTests` — 5 tests covering pass-through, compute, halt, ALWAYS, socket mismatch
- [ ] `FeatureEngineerBlock` unmodified and still registers as block 8 in MLTraining group
