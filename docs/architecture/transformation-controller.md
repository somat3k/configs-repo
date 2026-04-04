# Transformation Controller Framework

> **Reference**: [Designer Block Graph](designer-block-graph.md) | [Module Topology](module-topology.md) | [Session Schedule](../session-schedule.md)
> **Skills**: `.skills/system-architect.md` · `.skills/dotnet-devs.md` · `.skills/websockets-inferences.md`

---

## Overview

The **Transformation Controller** (TC) is a framework-level component that acts as
the authoritative routing and transformation layer between all block sub-divisions
within a strategy graph.  It provides:

1. **Unified payload envelope** — a single structured container that carries both
   the original signal data and an attached `TransformationUnit` describing all
   transformations applied to that data as it traversed the graph.
2. **Transformation pipeline** — an ordered chain of `ITransformationUnit`
   implementations that each block registers before its output is forwarded downstream.
3. **Sub-division support** — structured routing so that a parent graph can delegate
   signal processing to named sub-divisions (e.g. `"risk"`, `"execution"`, `"ml"`)
   and receive back a transformed payload without knowing the internal topology.
4. **Audit trail** — every transformation step is logged with its block ID, type,
   and timestamp, enabling full trace-back from any output to its origin.

---

## Core Contracts

### `TransformationUnit`

```csharp
namespace MLS.Core.Designer;

/// <summary>
/// Describes a single transformation applied to a BlockSignal as it passed
/// through a specific block.  Attached to the envelope so downstream consumers
/// know the full processing history of the signal.
/// </summary>
public sealed record TransformationUnit(
    /// <summary>The block that applied this transformation.</summary>
    Guid BlockId,

    /// <summary>Human-readable type name of the block (e.g. "FeatureEngineerBlock").</summary>
    string BlockType,

    /// <summary>Named sub-division this block belongs to (e.g. "ml", "risk", "defi").</summary>
    string SubDivision,

    /// <summary>UTC timestamp when the transformation was applied.</summary>
    DateTimeOffset AppliedAt,

    /// <summary>
    /// Optional key-value metadata describing what was changed.
    /// E.g. { "rows_dropped": 12, "features_added": ["RSI", "MACD"] }
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata
);
```

### `TransformationEnvelope`

```csharp
/// <summary>
/// Unified envelope wrapper that carries the original BlockSignal together with
/// the ordered list of TransformationUnits accumulated during graph traversal.
/// </summary>
public sealed record TransformationEnvelope(
    /// <summary>The signal payload at its current (most-recently-transformed) state.</summary>
    BlockSignal Signal,

    /// <summary>
    /// Ordered list of transformations applied since the signal originated.
    /// First element = source block; last element = most recent transformer.
    /// </summary>
    IReadOnlyList<TransformationUnit> TransformationHistory
)
{
    /// <summary>
    /// Append a new transformation unit and return a new envelope.
    /// Envelopes are immutable; this produces a new instance with the updated history.
    /// </summary>
    public TransformationEnvelope WithTransformation(TransformationUnit unit) =>
        this with { TransformationHistory = [..TransformationHistory, unit] };
}
```

### `ITransformationController`

```csharp
/// <summary>
/// Central coordinator responsible for routing TransformationEnvelopes through
/// the block graph, dispatching to the correct sub-division, and collecting results.
/// </summary>
public interface ITransformationController
{
    /// <summary>
    /// Route an envelope through the specified sub-division.
    /// Returns the transformed envelope after all blocks in that sub-division
    /// have processed the signal.
    /// </summary>
    ValueTask<TransformationEnvelope> RouteAsync(
        TransformationEnvelope envelope,
        string subDivision,
        CancellationToken ct);

    /// <summary>
    /// Register a block as a participant in a named sub-division.
    /// Blocks are processed in registration order within each sub-division.
    /// </summary>
    void RegisterBlock(string subDivision, IBlockElement block);

    /// <summary>
    /// Retrieve the full transformation history for a signal by its origin ID.
    /// Used for audit and debugging.
    /// </summary>
    IReadOnlyList<TransformationUnit> GetHistory(Guid originSignalId);
}
```

---

## Sub-Division Architecture

Sub-divisions are named logical groupings within a strategy graph.  Each sub-division
contains an ordered set of blocks that are applied in sequence to an incoming envelope:

```
TransformationController
│
├── "data"       ─▶ [DataLoaderBlock, FeatureEngineerBlock, TrainSplitBlock]
├── "ml"         ─▶ [TrainModelBlock, ValidateModelBlock, ExportONNXBlock]
├── "risk"       ─▶ [CollateralHealth, LendingHealthBlock, LiquidationGuard]
├── "execution"  ─▶ [SpreadCalculatorBlock, ProfitGateBlock]
└── "defi"       ─▶ [MorphoSupply, BalancerSwap, YieldOptimizer]
```

The parent graph calls `RouteAsync(envelope, "ml")` and receives back a fully
transformed envelope with the `TransformationHistory` updated by each ML block in sequence.

---

## Unified Payload Envelope

The `TransformationEnvelope` extends the MLS wire-level `Envelope` protocol with
the `transformation_history` field in the payload section:

```json
{
  "type": "BLOCK_SIGNAL",
  "version": 1,
  "session_id": "...",
  "payload": {
    "signal_type": "FeatureVector",
    "transformation_history": [
      {
        "block_id": "...",
        "block_type": "DataLoaderBlock",
        "sub_division": "data",
        "applied_at": "2026-04-04T10:00:00Z",
        "metadata": { "rows_loaded": 2048, "symbol": "ETH/USDC" }
      },
      {
        "block_id": "...",
        "block_type": "FeatureEngineerBlock",
        "sub_division": "data",
        "applied_at": "2026-04-04T10:00:00.012Z",
        "metadata": { "features": ["RSI","MACD","BB","ATR","VWAP","VolDelta","SpreadBps","VwapDist"], "rows_dropped": 14 }
      }
    ],
    "features_ref": "ipfs://Qm...",
    "n_rows": 2034,
    "n_features": 8
  }
}
```

---

## BlockBase Integration

`BlockBase` is extended with a helper that automatically wraps its output signal
in a `TransformationEnvelope` and appends the transformation unit before forwarding:

```csharp
// In BlockBase:
protected async ValueTask EmitTransformedAsync(
    TransformationEnvelope incoming,
    BlockSignal outgoing,
    IReadOnlyDictionary<string, object>? metadata,
    CancellationToken ct)
{
    var unit = new TransformationUnit(
        BlockId:     BlockId,
        BlockType:   BlockType,
        SubDivision: SubDivision,
        AppliedAt:   DateTimeOffset.UtcNow,
        Metadata:    metadata
    );
    var outEnvelope = incoming.WithTransformation(unit) with { Signal = outgoing };
    await EmitSignalAsync(outEnvelope.Signal, ct).ConfigureAwait(false);
    // Controller stores outEnvelope in history for audit
    _controller?.RecordEnvelope(outEnvelope);
}
```

---

## Session Schedule Integration

| Session | Deliverable |
|---------|------------|
| Session 07 | `ITransformationController`, `TransformationUnit`, `TransformationEnvelope` in `MLS.Core.Designer` |
| Session 07 | `TransformationController` concrete implementation in `MLS.Designer.Services` |
| Session 07 | `BlockBase.EmitTransformedAsync` helper method |
| Session 07 | Sub-division constants in `MLS.Core.Constants.SubDivision` |
| Session 08 | AI Hub plugin: `GetTransformationHistoryPlugin` — SK function that returns the envelope history for the AI to explain the signal's processing chain to the user |
| Session 19+ | Roslyn-compiled custom blocks automatically register with the TC under a `"user"` sub-division |

---

## Sub-Division Constants

```csharp
namespace MLS.Core.Constants;

/// <summary>Named sub-divisions for the Transformation Controller.</summary>
public static class SubDivision
{
    public const string Data      = "data";
    public const string MachineLearning = "ml";
    public const string Risk      = "risk";
    public const string Execution = "execution";
    public const string DeFi      = "defi";
    public const string Arbitrage = "arbitrage";
    public const string User      = "user";      // Roslyn-compiled user blocks
}
```

---

## Acceptance Criteria

- [ ] `TransformationUnit`, `TransformationEnvelope`, `ITransformationController` compile in `MLS.Core`
- [ ] `SubDivision` constants in `MLS.Core.Constants`
- [ ] `TransformationController` routes envelopes through registered block chains
- [ ] `BlockBase.EmitTransformedAsync` appends transformation unit to envelope history
- [ ] `TransformationEnvelope` serialises to JSON with `transformation_history` array
- [ ] Unit test: `TransformationControllerTests` — 4 tests: register, route single block, route chain, history retrieval
- [ ] Integration test: `MLTrainingSubDivisionTests` — route a `FeatureVector` signal through the `"data"` + `"ml"` sub-divisions; assert history contains both `DataLoaderBlock` and `FeatureEngineerBlock` units
