# ml-runtime — Session 3: C# Inference Session

## C# Inference Session

When generating C# inference code:
1. Use `Microsoft.ML.OnnxRuntime` with parallel execution mode
2. Pre-load models on startup, support hot-reload
3. Target < 10ms inference latency
4. Expose streaming inference via SignalR
5. Cache inference results in Redis (configurable TTL)
