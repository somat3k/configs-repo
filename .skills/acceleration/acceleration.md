---
name: acceleration
component: system
description: 'L1/L2/L3/L4 process acceleration methodology and CPU thread control for the MLS platform — covers single-thread vectorisation (L1), multi-thread CPU parallelism (L2), multi-process distributed training (L3), and hardware-level acceleration (L4). Applied to both C# infrastructure and Python ML training pipelines.'
---

# System Acceleration — MLS Trading Platform

> This component is separate from the model components (`model-t`, `model-a`, `model-d`).
> It governs **platform-level** performance: how the system processes data and computes, not what it predicts.

---

## Acceleration Levels Overview

| Level | Name | Scope | Mechanism |
|-------|------|-------|-----------|
| **L1** | Single-Thread Vectorisation | Per-process | SIMD, AVX, NumPy, `torch.compile` |
| **L2** | Multi-Thread CPU Parallelism | Single machine | `ThreadPool`, `DataLoader(num_workers)`, `torch.set_num_threads` |
| **L3** | Multi-Process / Distributed | Single machine or cluster | `torch.multiprocessing`, `DistributedDataParallel`, `Aspire` orchestration |
| **L4** | Hardware Acceleration | GPU / accelerator | CUDA, mixed precision (AMP), `torch.compile` with GPU backend |

---

## L1 — Single-Thread Vectorisation

### Python (PyTorch)
```python
import torch

# torch.compile — JIT-fuses operations into vectorised kernels (PyTorch 2+)
# mode="reduce-overhead": best for repeated small-batch inference (< 100ms latency target)
# mode="max-autotune":   best for throughput-bound training
model_compiled = torch.compile(model, mode="reduce-overhead", fullgraph=True)

# Control thread count for L1 — one thread per physical core for inference
torch.set_num_threads(1)          # disable intra-op parallelism at L1
torch.set_num_interop_threads(1)  # disable inter-op parallelism at L1

# Vectorised feature computation with broadcasting (no Python loops)
def vectorised_features(prices: torch.Tensor) -> torch.Tensor:
    """Compute RSI, momentum, and log-returns in one vectorised pass."""
    log_ret = torch.log(prices[1:] / prices[:-1])
    momentum = prices[-1] / prices[-20] - 1
    delta = torch.diff(prices)
    gain = torch.clamp(delta, min=0).mean()
    loss = torch.clamp(-delta, min=0).mean()
    rsi = 100 - (100 / (1 + gain / (loss + 1e-8)))
    return torch.stack([log_ret[-1], momentum, rsi / 100])
```

### C# (SIMD / System.Runtime.Intrinsics)
```csharp
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

// AVX2 vectorised dot product — used in feature scaling hot path
public static float DotProductAvx2(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
{
    if (!Avx2.IsSupported) return DotProductScalar(a, b);

    var sum = Vector256<float>.Zero;
    int i = 0;
    for (; i <= a.Length - 8; i += 8)
    {
        var va = Vector256.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(a[i..]));
        var vb = Vector256.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(b[i..]));
        sum = Avx.Add(sum, Avx.Multiply(va, vb));
    }
    // Horizontal sum of 8 floats
    var hi = Avx.ExtractVector128(sum, 1);
    var lo = Avx.ExtractVector128(sum, 0);
    var s = Sse.Add(hi, lo);
    s = Sse.Add(s, Sse.MoveHighToLow(s, s));
    s = Sse.AddScalar(s, Sse.Shuffle(s, s, 1));
    float result = s.ToScalar();
    // Handle tail
    for (; i < a.Length; i++) result += a[i] * b[i];
    return result;
}
```

---

## L2 — Multi-Thread CPU Parallelism

### Python (PyTorch DataLoader + Thread Affinity)
```python
import os
import torch
from torch.utils.data import DataLoader

# Optimal thread count for training (leave 1-2 cores for OS + I/O)
physical_cores = os.cpu_count() // 2  # assume hyperthreading
torch.set_num_threads(physical_cores)
torch.set_num_interop_threads(2)       # 2 inter-op threads for pipeline stages

# DataLoader with pinned memory and multi-worker prefetch
def make_loader(dataset, batch_size: int, training: bool) -> DataLoader:
    return DataLoader(
        dataset,
        batch_size=batch_size,
        shuffle=training,
        num_workers=min(physical_cores, 8),  # L2 parallel data loading
        pin_memory=True,                     # zero-copy GPU transfer if available
        persistent_workers=True,             # keep workers alive between epochs
        prefetch_factor=4,                   # prefetch 4 batches per worker
    )

# CPU affinity — pin training workers to specific cores
def set_worker_affinity(worker_id: int):
    """Called by DataLoader in each worker process."""
    os.sched_setaffinity(0, {worker_id % physical_cores})

loader = make_loader(dataset, batch_size=512, training=True)
# Pass worker_init_fn=set_worker_affinity to DataLoader for core pinning
```

### C# (Thread Pool + Channel-based Parallelism)
```csharp
using System.Threading.Channels;
using System.Collections.Concurrent;

// Bounded channel for backpressure — prevents memory blowup under load
var channel = Channel.CreateBounded<FeatureVector>(new BoundedChannelOptions(1024)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleWriter = false,
    SingleReader = false,
});

// Pin inference threads to specific CPU cores for cache locality
public static void PinThreadToCore(int coreIndex)
{
    var thread = Thread.CurrentThread;
    // Windows: use SetThreadAffinityMask via P/Invoke
    // Linux: use sched_setaffinity via P/Invoke
    thread.Priority = ThreadPriority.AboveNormal;
}

// Parallel inference with bounded concurrency
var semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);
var inferenceBlock = new ActionBlock<FeatureVector>(
    async features =>
    {
        await semaphore.WaitAsync();
        try { await RunInferenceAsync(features); }
        finally { semaphore.Release(); }
    },
    new ExecutionDataflowBlockOptions
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount / 2,
        BoundedCapacity = 512,
    }
);
```

---

## L3 — Multi-Process / Distributed Training

### Python (DistributedDataParallel)
```python
import torch.distributed as dist
import torch.multiprocessing as mp
from torch.nn.parallel import DistributedDataParallel as DDP


def setup_distributed(rank: int, world_size: int):
    os.environ["MASTER_ADDR"] = "localhost"
    os.environ["MASTER_PORT"] = "12355"
    dist.init_process_group("gloo", rank=rank, world_size=world_size)  # CPU: gloo; GPU: nccl
    torch.set_num_threads(max(1, os.cpu_count() // world_size))        # divide cores evenly


def train_distributed(rank: int, world_size: int, model: nn.Module, dataset):
    setup_distributed(rank, world_size)
    model = DDP(model)  # wraps model for gradient synchronisation
    sampler = torch.utils.data.distributed.DistributedSampler(
        dataset, num_replicas=world_size, rank=rank, shuffle=True
    )
    loader = DataLoader(dataset, batch_size=256, sampler=sampler, num_workers=4)
    # ... training loop ...
    dist.destroy_process_group()


def launch_distributed(model: nn.Module, dataset, n_processes: int = 4):
    """Launch n_processes training workers — one per CPU socket or GPU."""
    mp.spawn(train_distributed, args=(n_processes, model, dataset), nprocs=n_processes)
```

### C# (.NET Aspire Multi-Instance)
```csharp
// In MLS.AppHost — scale out ml-runtime to N replicas for distributed inference
var mlRuntime = builder.AddProject<Projects.MLRuntime>("ml-runtime")
    .WithReplicas(4)                           // L3: 4 parallel inference processes
    .WithEnvironment("TORCH_NUM_THREADS", "4")
    .WithReference(redis)
    .WithReference(postgres);
```

---

## L4 — Hardware Acceleration

### Python (Mixed Precision + torch.compile)
```python
from torch.cuda.amp import autocast, GradScaler

scaler = GradScaler()  # handles gradient scaling for FP16

def training_step_l4(model, batch, optimizer, device: str = "cuda"):
    """L4 training step with AMP (Automatic Mixed Precision)."""
    x, y = batch[0].to(device), batch[1].to(device)

    # Forward pass in FP16 (2x memory saving, ~1.5-3x throughput on Tensor Cores)
    with autocast(device_type=device, dtype=torch.float16):
        logits, confidence = model(x)
        loss = criterion(logits, y)

    # Backward pass with gradient scaling to prevent underflow
    scaler.scale(loss).backward()
    scaler.unscale_(optimizer)
    torch.nn.utils.clip_grad_norm_(model.parameters(), max_norm=1.0)  # gradient clipping
    scaler.step(optimizer)
    scaler.update()
    optimizer.zero_grad(set_to_none=True)  # set_to_none=True: free memory faster than zero_


# torch.compile with max-autotune for GPU — fuses ops into CUDA kernels
model_l4 = torch.compile(
    model,
    mode="max-autotune",   # exhaustive kernel search (slow compile, fast runtime)
    backend="inductor",    # Triton-based code generation
    dynamic=False,         # static shapes for maximum kernel fusion
)
```

### CPU-Only L4 (no GPU — production server inference)
```python
# Use OpenMP backend for CPU inference (often faster than default GOMP)
os.environ["OMP_NUM_THREADS"] = str(physical_cores)
os.environ["MKL_NUM_THREADS"] = str(physical_cores)
os.environ["KMP_AFFINITY"] = "granularity=fine,compact,1,0"  # Intel thread affinity

# torch.compile for CPU — uses C++ code generation via Inductor
model_cpu_optimised = torch.compile(
    model.float(),
    mode="reduce-overhead",
    backend="inductor",
    dynamic=False,
)

# INT8 static quantisation (4x model size reduction, ~2x CPU inference speedup)
from torch.quantization import quantize_dynamic
model_int8 = quantize_dynamic(
    model, {nn.Linear}, dtype=torch.qint8
)
```

---

## Acceleration Selection Guide

```python
def select_acceleration_level(
    latency_target_ms: float,
    throughput_target_rps: float,
    has_gpu: bool,
    n_cpu_cores: int,
) -> str:
    """Return recommended acceleration level for given requirements."""
    if latency_target_ms < 1:
        return "L1"   # Sub-millisecond: single-thread vectorised only
    if latency_target_ms < 5 and not has_gpu:
        return "L2"   # Low-latency CPU: multi-thread parallel
    if throughput_target_rps > 10_000 and not has_gpu:
        return "L3"   # High-throughput CPU: multi-process
    if has_gpu:
        return "L4"   # GPU available: use AMP + torch.compile
    return "L2"       # Default: multi-thread CPU
```

---

## C# Infrastructure Acceleration Summary

| Pattern | Level | When to Use |
|---------|-------|-------------|
| SIMD / AVX2 intrinsics | L1 | Feature computation hot path (< 1ms) |
| `System.Threading.Channels` bounded | L2 | Inter-module message queues |
| `ActionBlock<T>` with bounded capacity | L2 | Parallel inference fan-out |
| `.NET Aspire` multi-replica | L3 | Scale out inference workers |
| `ArrayPool<T>` / `MemoryPool<T>` | L1 | Eliminate GC on hot paths |
| `ThreadPool.UnsafeQueueUserWorkItem` | L2 | Fire-and-forget micro-tasks |
| ONNX `ORT_PARALLEL` + `IntraOpNumThreads` | L2/L4 | ONNX Runtime threading |
