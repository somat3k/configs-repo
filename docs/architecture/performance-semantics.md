> ✅ **Status: Complete** — Implemented and verified in session 23 (workflow-demo).

# Performance Semantics — MLS Platform

> **Reference**: [Giga-Scale Plan](giga-scale-plan.md) | [Session Schedule](../session-schedule.md) (Sessions 02, 22)
> **Skills**: `.skills/acceleration/acceleration.md` · `.skills/beast-development.md`

---

## L1–L4 Acceleration Applied Per Layer

### Overview

| Level | Name | Scope | Mechanism | MLS Application |
|-------|------|-------|-----------|----------------|
| **L1** | Single-Thread Vectorisation | Per-process | SIMD, AVX2, `System.Numerics.Vector<T>`, `torch.compile` | Feature engineering, indicator blocks, envelope parsing |
| **L2** | Multi-Thread CPU Parallelism | Single machine | `Parallel.ForEachAsync`, `Channel<T>`, `DataLoader(num_workers)` | Block graph fan-out, gap detection, training data loading |
| **L3** | Multi-Process / Distributed | Single machine or cluster | Aspire replicas, DDP, `torch.multiprocessing` | ML training, inference worker pool, BC replicas |
| **L4** | Hardware Acceleration | GPU / accelerator | CUDA, AMP bf16, `torch.compile` GPU backend, ONNX GPU EP | Model training, batch ONNX inference |

---

## L1 — Single-Thread Vectorisation

### C# — SIMD Feature Engineering

```csharp
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

/// <summary>
/// AVX2-vectorised RSI computation for a sliding window.
/// Processes 8 float32 values per instruction.
/// Target: &lt; 100ns per single-candle RSI update.
/// </summary>
public static float ComputeRSI(ReadOnlySpan<float> prices, int period = 14)
{
    if (!Avx2.IsSupported)
        return ComputeRSIScalar(prices, period);  // Fallback

    // Vectorised delta computation using AVX2
    // delta[i] = prices[i+1] - prices[i]
    // Partition deltas into gains (clamped min=0) and losses (clamped max=0)
    // Average gains / average losses → RSI = 100 - 100/(1 + avg_gain/avg_loss)
}
```

### C# — Zero-Alloc Envelope Routing Hot Path

```csharp
// Zero allocations: ArrayPool<byte>, Span<byte>, pre-parsed MessagePack
// Topic lookup: O(1) ConcurrentDictionary (no LINQ)
// Subscriber list: ImmutableHashSet<Guid> (lock-free reads)

public async ValueTask RouteAsync(ReadOnlyMemory<byte> rawMessage, CancellationToken ct)
{
    // Parse topic from MessagePack header without full deserialization
    var topic = MessagePackTopicExtractor.Extract(rawMessage.Span);    // Span, no alloc

    if (!_subscriptions.TryGetValue(topic, out var subscribers)) return;

    // Fan-out to all subscribers (ValueTask, no boxing)
    foreach (var subscriberId in subscribers)
    {
        await _connections[subscriberId].SendAsync(rawMessage, ct);
    }
}
```

### Python — torch.compile

```python
# L1: JIT-fuse operations into vectorised kernels (PyTorch 2+)
# mode="reduce-overhead": best for repeated small-batch inference (< 100ms target)
# mode="max-autotune": best for throughput-bound training
model_inference = torch.compile(model, mode="reduce-overhead", fullgraph=True)
model_training  = torch.compile(model, mode="max-autotune")

# L1: Disable intra-op parallelism at L1 for inference (1 thread = minimal overhead)
torch.set_num_threads(1)
torch.set_num_interop_threads(1)
```

---

## L2 — Multi-Thread CPU Parallelism

### C# — Block Graph Fan-Out

```csharp
/// <summary>
/// MultiplicationKernel: broadcasts one incoming candle to N indicator blocks in parallel.
/// Each block processes independently — no lock contention.
/// </summary>
public sealed class IndicatorKernel(IReadOnlyList<IBlockElement> _blocks)
{
    private readonly Channel<OHLCVCandle> _broadcast =
        Channel.CreateBounded<OHLCVCandle>(
            new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = true,
            });

    public async Task RunAsync(CancellationToken ct)
    {
        await Parallel.ForEachAsync(_blocks, ct, async (block, innerCt) =>
        {
            await foreach (var candle in _broadcast.Reader.ReadAllAsync(innerCt))
                await block.ProcessAsync(new BlockSignal(candle), innerCt).ConfigureAwait(false);
        });
    }
}
```

### C# — Gap Detection Parallel Scan

```csharp
// All active feeds checked concurrently
await Parallel.ForEachAsync(activeFeeds, ct, async (feed, innerCt) =>
{
    await DetectGapForFeedAsync(feed, innerCt);
});
```

### Python — DataLoader Workers

```python
# L2: DataLoader with multiple worker processes for training
# pin_memory=True: DMA-pin host memory for faster GPU transfer (L4)
dataloader = DataLoader(
    dataset,
    batch_size=256,
    num_workers=4,          # 4 CPU workers preprocessing in parallel
    pin_memory=True,        # async GPU memory copy
    persistent_workers=True,
    prefetch_factor=2,
)
```

---

## L3 — Multi-Process / Distributed

### Aspire Service Replicas

```csharp
// AppHost: scale inference workers horizontally
var mlRuntime = builder.AddProject<MLS.MLRuntime>("ml-runtime")
    .WithReplicas(4)             // 4 parallel inference workers
    .WithEnvironment("ASPNETCORE_URLS", "http://+:5600");

var blockController = builder.AddProject<MLS.BlockController>("block-controller")
    .WithReplicas(2)             // Primary + hot standby
    .WithReference(mlRuntime);
```

### Python — DistributedDataParallel (Multi-GPU Training)

```python
# L3: DDP for multi-GPU training when hardware available
# Aspire detects replica count via ASPIRE_REPLICA_INDEX env var
import os
import torch.distributed as dist
from torch.nn.parallel import DistributedDataParallel as DDP

if int(os.environ.get('ASPIRE_REPLICA_COUNT', '1')) > 1:
    dist.init_process_group(backend='nccl')
    model = DDP(model, device_ids=[local_rank])
```

---

## L4 — Hardware Acceleration

### ONNX Runtime GPU Execution Provider

```csharp
// ML Runtime: use CUDA execution provider when GPU available, fallback to CPU
var sessionOptions = new SessionOptions();

if (IsCudaAvailable())
{
    sessionOptions.AppendExecutionProvider_CUDA(new OrtCUDAProviderOptions
    {
        DeviceId = 0,
        CudnnConvAlgoSearch = OrtCudnnConvAlgoSearch.Exhaustive,
    });
}
else
{
    sessionOptions.AppendExecutionProvider_CPU();
}

// Pre-allocate OrtValue tensors — reuse across inference calls (zero alloc on hot path)
var inputTensor = OrtValue.CreateTensorValueFromMemory(
    _preallocatedBuffer, new long[] { 1, 8 });
```

### Python — AMP Mixed Precision Training

```python
# L4: Automatic Mixed Precision (bf16 preferred over fp16 for stability)
scaler = torch.cuda.amp.GradScaler(enabled=torch.cuda.is_available())

for batch_features, batch_labels in dataloader:
    optimizer.zero_grad(set_to_none=True)    # set_to_none saves memory

    with torch.autocast(device_type='cuda', dtype=torch.bfloat16):
        outputs = model(batch_features)
        loss = criterion(outputs, batch_labels)

    scaler.scale(loss).backward()
    scaler.unscale_(optimizer)
    torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
    scaler.step(optimizer)
    scaler.update()
```

---

## Serialization Strategy

### Wire Protocol: MessagePack (Binary)

```csharp
// All envelope messages on the wire use MessagePack — ~3× smaller than JSON
// Source-generated for AOT compatibility
[MessagePackObject]
public sealed record EnvelopePayload(
    [property: Key(0)] string Type,
    [property: Key(1)] int Version,
    [property: Key(2)] Guid SessionId,
    [property: Key(3)] string ModuleId,
    [property: Key(4)] DateTimeOffset Timestamp,
    [property: Key(5)] byte[] Payload    // inner type serialized separately
);
```

### Config / Schema: System.Text.Json (Source-Generated)

```csharp
// Strategy schemas, block parameters, API responses
[JsonSerializable(typeof(StrategyGraphPayload))]
[JsonSerializable(typeof(BlockParameter[]))]
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
internal partial class DesignerJsonContext : JsonSerializerContext { }
```

### ML Features: Apache Arrow (Zero-Copy)

```python
# Training pipeline: Arrow feather format via IPFS
# Zero-copy Pandas DataFrame from Arrow buffer
import pyarrow.feather as feather
import pandas as pd

# Write features to IPFS-hosted feather file
feather.write_feather(df_features, '/tmp/features_v3.feather', compression='lz4')

# Load with zero-copy Arrow buffer
table = feather.read_table('/tmp/features_v3.feather', memory_map=True)
df = table.to_pandas(zero_copy_only=True)
```

---

## Parallelization Patterns

### Computation Circuit (Beast Pattern)

```csharp
/// <summary>
/// A pipeline of processing stages connected by typed channels.
/// Each stage runs on its own thread. Back-pressure via bounded channels.
/// </summary>
public class ComputationCircuit<TIn, TOut>
{
    // Input channel: receives raw data (e.g. OHLCV candles)
    private readonly Channel<TIn> _inputChannel =
        Channel.CreateBounded<TIn>(new BoundedChannelOptions(2048)
            { FullMode = BoundedChannelFullMode.DropOldest });

    // Bus stops: intermediate buffering between stages
    // BoundedChannelOptions with explicit FullMode = key performance tuning point
    private readonly Channel<ProcessingContext>[] _busStops;

    // Output channel: emits typed results (e.g. trade signals)
    private readonly Channel<TOut> _outputChannel =
        Channel.CreateBounded<TOut>(new BoundedChannelOptions(512)
            { FullMode = BoundedChannelFullMode.Wait });
}
```

### Object Pooling

```csharp
// Pool frequently-allocated objects to eliminate GC pressure on hot path
private static readonly ObjectPool<EnvelopePayload> _payloadPool =
    new DefaultObjectPoolProvider().Create<EnvelopePayload>();

private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

// Usage on hot path:
var buffer = _bufferPool.Rent(4096);
try { /* work */ }
finally { _bufferPool.Return(buffer, clearArray: false); }
```

---

## Replication Strategy

| Module | Replication | Rationale |
|--------|-------------|-----------|
| Block Controller | 2× Aspire replicas (primary + hot standby) | Central routing — must be HA |
| ML Runtime | N× inference workers (horizontally scaled) | Throughput — parallel inference |
| Data Layer | PostgreSQL streaming replication → Npgsql read-replica routing | Read-heavy — separate read path |
| AI Hub | Stateless (provider router) → horizontally scalable | LLM calls are long-lived, parallelize |
| Trader | Single-writer (order serialization) | Race condition prevention |
| Designer | Single instance per session | Canvas state consistency |

---

## Runtime Optimization Defaults

```xml
<!-- All backend service projects -->
<PropertyGroup>
  <!-- PGO: Profile-Guided Optimization (JIT learns hot paths) -->
  <PublishReadyToRun>true</PublishReadyToRun>
  <TieredCompilation>true</TieredCompilation>
  <TieredPGO>true</TieredPGO>

  <!-- Server GC: multi-core, higher throughput, larger gen0 -->
  <!-- Set via runtimeconfig.json or environment:              -->
  <!-- DOTNET_GCConserve=0, DOTNET_GCServer=1                  -->
</PropertyGroup>
```

```json
// runtimeconfig.template.json (all backend services)
{
  "configProperties": {
    "System.GC.Server": true,
    "System.GC.HeapHardLimit": 2147483648,
    "System.GC.LOHCompactionMode": 2,
    "System.Threading.ThreadPool.MinThreads": 16,
    "System.Net.Http.SocketsHttpHandler.Http2Support": true
  }
}
```

```bash
# Python training environment
export PYTHONOPTIMIZE=2
export OMP_NUM_THREADS=4          # OpenMP threads for NumPy
export OPENBLAS_NUM_THREADS=4
export MKL_NUM_THREADS=4
```

---

## Performance Benchmark Targets

| Operation | Instrument | Target | Level |
|-----------|------------|--------|-------|
| Envelope parse + route | BenchmarkDotNet median | < 1µs | L1 |
| RSI(14) single candle update | BenchmarkDotNet median | < 100ns | L1 |
| MACD full compute | BenchmarkDotNet median | < 500ns | L1 |
| Feature vector (8 features, 200 candles) | BenchmarkDotNet median | < 1ms | L1 |
| model-t ONNX inference | BenchmarkDotNet p95 | < 10ms | L1/L4 |
| Subscription table lookup | BenchmarkDotNet median | < 200ns | L1 |
| Strategy deploy (100 blocks) | BenchmarkDotNet median | < 5ms | L2 |
| Envelope routing allocations | BenchmarkDotNet alloc | 0 bytes | L1 |
| Python model-t inference | pytest benchmark median | < 5ms | L1/L4 |

All benchmark results recorded in `docs/architecture/performance-baselines.md` after Session 22.

---

## See Also

- [Session Schedule — Sessions 02, 22](../session-schedule.md)
- `.skills/acceleration/acceleration.md` — full L1–L4 code examples
- `.skills/beast-development.md` — computation circuits, pooling, kernel multiplication
- [Giga-Scale Plan — Performance Architecture](giga-scale-plan.md#performance-architecture)
