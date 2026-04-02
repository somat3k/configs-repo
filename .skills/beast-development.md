---
name: beast-development
source: custom (MLS Trading Platform)
description: 'High-performance, enterprise-grade C# development — computation circuits, bus stops, pooling, resource management, kernel multiplication, and processing system design for ultra-low latency trading.'
---

# Beast Development — High-Performance MLS Trading Platform

## Philosophy
- **No fake simulations** — all code must be production-ready and functional
- **No placeholder implementations** — every method must be fully implemented
- **Lean and mean** — minimal allocations, maximum throughput
- **Measurable performance** — every critical path has benchmarks

## Computation Circuits
A "computation circuit" is a pipeline of processing stages connected by typed channels:
```csharp
public class ComputationCircuit<TIn, TOut>
{
    private readonly Channel<TIn> _inputChannel;
    private readonly Channel<TOut> _outputChannel;
    private readonly IProcessor<TIn, TOut>[] _stages;
    
    // Bus stop: intermediate buffering point between stages
    private readonly Channel<ProcessingContext>[] _busStops;
}
```

## Resource Pooling
```csharp
// Object pooling for high-frequency allocations
private static readonly ObjectPool<EnvelopePayload> _payloadPool =
    new DefaultObjectPoolProvider().Create<EnvelopePayload>();

// Buffer pooling for network operations
private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

// Thread pool work items for micro-service kernels
ThreadPool.QueueUserWorkItem(static state => ProcessKernelWork(state), workItem, true);
```

## Micro-Service Multiplication Kernel
The kernel multiplexes incoming data streams to multiple processing units:
```csharp
public class MultiplicationKernel<T>
{
    private readonly IReadOnlyList<IProcessingUnit<T>> _units;
    private readonly Channel<T> _broadcastChannel;
    
    public async ValueTask BroadcastAsync(T item, CancellationToken ct)
    {
        var tasks = _units.Select(u => u.ProcessAsync(item, ct));
        await Task.WhenAll(tasks); // Parallel processing
    }
}
```

## Performance Standards
- Trade signal processing: < 1ms end-to-end
- Market data ingestion: > 100,000 ticks/second
- ML inference: < 10ms per prediction
- Database write throughput: > 10,000 records/second (batch insert)
- Memory: no GC pressure on hot paths (use value types + pooling)

## Benchmarking
- Use `BenchmarkDotNet` for all performance-critical code paths
- Benchmarks live in `src/benchmarks/`
- CI pipeline runs benchmarks and fails on regression > 5%
- Profile with `dotnet-trace` and `dotnet-counters`

## Enterprise Patterns
- Use `IHostedService` for all background processing workers
- Implement graceful shutdown with `CancellationToken` propagation
- Use `System.Threading.Channels` for all producer/consumer scenarios
- Apply `SpinLock` / `Interlocked` for ultra-hot lock-free paths
- Use SIMD intrinsics (`System.Runtime.Intrinsics`) for numerical batch operations
