# Session 07 — Block Action Tiles: Block-as-ONE Data Source Pattern

> **Reference**: [Designer Block Graph](designer-block-graph.md) | [Session Schedule](../session-schedule.md) (Session 07)
> **Skills**: `.skills/designer.md` · `.skills/websockets-inferences.md` · `.skills/networking.md`

---

## Overview

The DeFi blocks introduced in Session 06 (`LendingHealthBlock`, `CollateralHealth`,
`YieldOptimizer`, etc.) emit signals per-event but are currently **passive** — they
process an input signal and emit an output signal.  They cannot be positioned as
**autonomous data sources** on the canvas: blocks that continuously fetch, store,
and re-stream their data to any number of connected tiles.

This document specifies the **Block-as-ONE** action tile expansion: every registered
block can optionally act as a **singleton data source** — a single live instance
whose current state is available to any tile that subscribes to it via a canvas
connection.

---

## Block-as-ONE Concept

```
Traditional block flow (Session 01–06):
  Input → Block → Output  (reactive, per-signal)

Block-as-ONE pattern (Session 07+):
  ┌──────────────────────────────────────┐
  │  LendingHealthBlock (singleton)       │
  │  ┌──────────────────────────────────┐│
  │  │  Internal state: latest HF data  ││
  │  │  Fetch loop: every N seconds     ││
  │  └──────────────────────────────────┘│
  │  Output socket: HealthFactorUpdate   │
  └──────────────────────────────────────┘
          │        │        │
          ▼        ▼        ▼
    TileA        TileB    TileC    (multiple consumers)
```

Any tile connected to the ONE block's output socket receives:
1. The **current state** on connect (snapshot).
2. All **future state updates** as they are emitted (streaming).

---

## Action Tile Interface

```csharp
namespace MLS.Core.Designer;

/// <summary>
/// Extends <see cref="IBlockElement"/> with autonomous data-source behaviour.
/// An action tile maintains internal state, runs a background fetch loop,
/// and streams data to all connected output sockets independently of whether
/// it has received an input signal.
/// </summary>
public interface IActionTile : IBlockElement
{
    /// <summary>
    /// True when the tile is running its internal fetch/update loop.
    /// Set by <see cref="StartAsync"/> and cleared by <see cref="StopAsync"/>.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Start the autonomous fetch/update loop.
    /// Called by the Designer engine when the strategy graph is activated.
    /// </summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Stop the fetch/update loop gracefully.</summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>
    /// Retrieve the current snapshot of this tile's internal state.
    /// Used to hydrate newly connected consumers without waiting for the next update.
    /// </summary>
    BlockSignal? GetCurrentSnapshot();
}
```

---

## Pass-Through Data as Default

Every action tile supports **pass-through** mode: if no output consumers are
connected, the tile still runs its loop but discards emitted signals.  When one or
more consumers connect, signals begin flowing to them automatically.

This removes the need for the canvas user to explicitly "start" data flow — connection
itself activates the data channel.

```csharp
// BlockBase (proposed extension)
protected virtual async ValueTask EmitToConnectedAsync(BlockSignal signal, CancellationToken ct)
{
    foreach (var socket in OutputSockets.Where(s => s.IsConnected))
    {
        await socket.EmitAsync(signal, ct).ConfigureAwait(false);
    }
    // If no consumers: signal is silently dropped (pass-through default)
}
```

---

## Block Register Conception

The `BlockRegistry` enforces that **each block type is ONE** — only a single live
instance of each block type may be active within a strategy graph at any time.
Multiple canvas tiles can subscribe to that ONE instance's output socket.

```csharp
public interface IBlockRegistry
{
    // Existing:
    IReadOnlyList<IBlockElement> GetAll();

    // New (Session 07):
    /// <summary>
    /// Get or create the singleton instance of a block type for the given strategy graph.
    /// If the block type is IActionTile and not yet started, StartAsync is called.
    /// </summary>
    ValueTask<IBlockElement> GetOrCreateSingletonAsync(
        string blockType,
        Guid strategyGraphId,
        CancellationToken ct);
}
```

---

## LendingHealthBlock as Action Tile

The `LendingHealthBlock` is the primary candidate for this pattern:

```csharp
/// <summary>
/// Upgraded LendingHealthBlock as an autonomous IActionTile.
/// Maintains one live health-factor snapshot per configured position.
/// Any number of canvas tiles (evaluation blocks, DeFi strategy blocks,
/// notification blocks) can subscribe to the streaming HealthFactorUpdate output.
/// </summary>
public sealed class LendingHealthBlock : BlockBase, IActionTile
{
    // ... existing composite score logic ...

    public bool IsRunning { get; private set; }

    public async Task StartAsync(CancellationToken ct)
    {
        IsRunning = true;
        await foreach (var update in FetchHealthUpdatesAsync(ct))
        {
            _snapshot = BuildSignal(update);
            await EmitToConnectedAsync(_snapshot, ct).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        IsRunning = false;
        return Task.CompletedTask;
    }

    public BlockSignal? GetCurrentSnapshot() => _snapshot;

    private BlockSignal? _snapshot;
}
```

---

## Data Fetch + Store Pattern

For blocks that need to persist their state across canvas sessions (e.g. persist the
last-known health factor so the canvas restores immediately on reconnect):

```csharp
// Data fetch + store loop (inside StartAsync):
await foreach (var update in FetchHealthUpdatesAsync(ct))
{
    // 1. Compute composite score
    var signal = BuildSignal(update);

    // 2. Store to Redis cache (fast restore on reconnect)
    await _cache.SetAsync(CacheKey, signal, ct).ConfigureAwait(false);

    // 3. Emit to all connected consumer tiles
    await EmitToConnectedAsync(signal, ct).ConfigureAwait(false);
}
```

---

## Envelope Integration

Action tile state updates are transported as standard `BLOCK_SIGNAL` envelopes so
the Block Controller can route them to subscribers in other modules (e.g. the web-app
can subscribe to the `HealthFactorUpdate` topic for real-time UI updates):

```json
{
  "type": "BLOCK_SIGNAL",
  "payload": {
    "signal_type": "HealthFactorUpdate",
    "block_type": "LendingHealthBlock",
    "graph_id": "...",
    "composite_score": 78.3,
    "severity": "Warning",
    "health_factor": 1.42,
    "ltv_ratio": 0.61,
    "liquidation_distance_pct": 12.5,
    "borrow_apr": 0.083
  }
}
```

---

## Acceptance Criteria (Session 07)

- [ ] `IActionTile` interface in `MLS.Core.Designer`
- [ ] `BlockRegistry.GetOrCreateSingletonAsync` enforces one-instance-per-type per graph
- [ ] `LendingHealthBlock` implements `IActionTile`: `StartAsync` / `StopAsync` / `GetCurrentSnapshot`
- [ ] Canvas consumer receives snapshot on connect, then streams updates
- [ ] `EmitToConnectedAsync` silently discards signals when no consumers are connected
- [ ] Integration test: `LendingHealthBlockActionTileTests` — connect/disconnect consumer, verify snapshot delivery and streaming
