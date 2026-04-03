---
name: designer
source: custom (MLS Trading Platform)
description: 'Block graph composer patterns for the MLS Designer module — IBlockElement lifecycle, socket type system, composition rules, strategy schema versioning, and StockSharp alignment mapping.'
---

# Designer — MLS Trading Platform

> Apply this skill when working on: block implementations, strategy schemas, exchange adapters, the nHOP path finder, ML training blocks, or any code in `MLS.Designer`.

---

## Core Abstractions

### IBlockElement Lifecycle

Every block follows this lifecycle — modelled on StockSharp's `BaseIndicator`:

```csharp
public interface IBlockElement
{
    Guid BlockId { get; }
    string BlockType { get; }
    IReadOnlyList<IBlockSocket> InputSockets { get; }
    IReadOnlyList<IBlockSocket> OutputSockets { get; }
    IReadOnlyList<BlockParameter> Parameters { get; }

    /// Called on each incoming signal during live execution
    ValueTask ProcessAsync(BlockSignal signal, CancellationToken ct);

    /// Called with historical data before live execution (backtest warm-up)
    Task PreloadAsync(IEnumerable<BlockSignal> historicalData, CancellationToken ct);

    /// Clear all internal state (equivalent to BaseIndicator.Reset())
    void Reset();
}
```

**Key Rules:**
- `BlockType` MUST match the registry key used in `IBlockRegistry.Register<T>(key)`
- `ProcessAsync` MUST be allocation-free on the hot path (use `ArrayPool`, `Span`, pre-computed state)
- `PreloadAsync` MUST call `Reset()` at start to clear any previously warmed state
- `Reset()` MUST clear ALL internal rolling-window state (arrays, running sums, counters)
- Every block MUST be `IAsyncDisposable` and clean up subscriptions in `DisposeAsync`

---

## Socket Type Rules

### Strict Type Safety

```csharp
// ✅ Correct: RSIBlock output (IndicatorValue) connects to ModelTInferenceBlock input (IndicatorValue)
await graph.ConnectAsync(rsiBlock.OutputSockets["indicator_output"].SocketId,
                          mlBlock.InputSockets["feature_input"].SocketId, ct);

// ❌ INVALID: CandleStream cannot connect to IndicatorValue input
// ConnectAsync throws InvalidBlockConnectionException with mismatched types
```

### Socket Naming Convention

- Input socket names: `{type}_input` (e.g. `candle_input`, `feature_input`, `signal_input`)
- Output socket names: `{type}_output` (e.g. `candle_output`, `indicator_output`, `ml_output`)
- Multi-socket blocks: `{type}_input_{n}` (e.g. `candle_input_a`, `candle_input_b` for spread calc)

---

## CompositeStrategyBlock (Fractal Nesting)

Based on StockSharp `CompositionDiagramElement` — disconnected inner sockets become outer ports:

```csharp
public sealed class CompositeStrategyBlock : IBlockElement, ICompositionGraph
{
    // Inner graph contains child blocks with internal connections
    // Disconnected inner sockets are EXPOSED as outer block sockets
    // This allows nesting: a CompositeBlock can contain other CompositeBlocks

    public IReadOnlyList<IBlockSocket> GetExposedPorts()
    {
        // Return all inner sockets that have no connection in the inner graph
        return _innerBlocks
            .SelectMany(b => b.InputSockets.Concat(b.OutputSockets))
            .Where(s => !_connections.Any(c => c.FromSocketId == s.SocketId || c.ToSocketId == s.SocketId))
            .ToList();
    }
}
```

---

## BlockParameter System

Parameters are typed, serializable, and optionally optimizable by HyperparamSearchBlock:

```csharp
// Declare parameters in block constructor
public sealed class RSIBlock : IBlockElement
{
    public IReadOnlyList<BlockParameter> Parameters { get; } = new[]
    {
        new BlockParameter<int>("Period", "Period", "RSI lookback period", 14, min: 2, max: 100, isOptimizable: true),
        new BlockParameter<Level1Fields?>("Source", "Source", "Price field to use", Level1Fields.ClosePrice),
    };
}
```

---

## Strategy Schema Versioning

```csharp
// ALWAYS increment SchemaVersion on any structural change
// Minor changes (parameter rename): increment by 1
// Breaking changes (socket removal, type change): increment by 10

public sealed class StrategySchema
{
    public Guid GraphId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SchemaVersion { get; set; }   // ALWAYS increment on mutation

    // Version history tracked automatically by EF Core temporal table
}
```

---

## nHOP Path Finder Rules

```
NO Uniswap: Only Camelot, DFYN, Balancer, Hyperliquid as exchange nodes
Max hops: 4 (configurable, default 4)
Min profit threshold: configurable, default $10 USD after gas
Graph edges: (tokenIn, tokenOut, exchange) → (price, liquidity, gas_estimate)
Algorithm: BFS + Bellman-Ford for negative cycles (arbitrage detection)
```

---

## Exchange Adapter Rules

- All blockchain addresses via `IBlockchainAddressBook` — NEVER hardcode address strings
- All DEX addresses use `BlockchainAddress` enum from `MLS.Core.Constants`
- Adapters MUST implement exponential backoff on connection failure (base 1s, max 60s)
- `ExecuteSwapAsync` MUST check slippage tolerance before submitting transaction
- `GetPriceAsync` latency target: < 100ms (cached in Redis with 1s TTL)

---

## Training Block Rules

- `TrainModelBlock` emits `TRAINING_JOB_START` — never calls Python directly
- Shell VM receives the envelope and spawns the Python process
- All training progress arrives as `TRAINING_JOB_PROGRESS` envelopes — never poll
- `ExportONNXBlock` receives `TRAINING_JOB_COMPLETE` and calls IPFS upload + model registry
- Feature schema version MUST match the deployed model's expected input dim

---

## Testing Requirements

```csharp
// Every block MUST have unit tests with known inputs → expected outputs
// Use static fixture OHLCV data (defined in test project, not random)

[Fact]
public async Task RSIBlock_KnownData_ReturnsCorrectRSI()
{
    // 14 known close prices → RSI should be within 0.01 of Python reference value
    var block = new RSIBlock();
    await block.PreloadAsync(TestFixtures.BtcCandles14, CancellationToken.None);
    var result = await block.ProcessAsync(new BlockSignal(TestFixtures.BtcCandle15), CancellationToken.None);
    result.IndicatorValue.Should().BeApproximately(TestFixtures.ExpectedRSI15, 0.01f);
}
```
