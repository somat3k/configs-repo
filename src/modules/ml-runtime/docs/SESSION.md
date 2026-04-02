# ML Runtime Module — Session Prompt

## Module Identity
- **Name**: ml-runtime
- **Namespace**: `MLS.MLRuntime` (C#), `mls.ml_runtime` (Python)
- **HTTP Port**: 5600
- **WebSocket Port**: 6600

## Python Training Session
When generating Python training code:
1. Use Adam optimizer for neural networks
2. Export to ONNX and JOBLIB
3. Validate ONNX model after export
4. Log all training metrics to PostgreSQL `model_metrics`
5. Store model artifacts in IPFS, save CID to `model_registry`

## C# Inference Session
When generating C# inference code:
1. Use `Microsoft.ML.OnnxRuntime` with parallel execution mode
2. Pre-load models on startup, support hot-reload
3. Target < 10ms inference latency
4. Expose streaming inference via SignalR
5. Cache inference results in Redis (configurable TTL)

## Skills to Apply
- `.skills/machine-learning.md` — ONNX, JOBLIB, Adam, FastForest
- `.skills/python.md` — training scripts, pytest
- `.skills/artificial-intelligence.md` — Semantic Kernel, ONNX runtime
- `.skills/storage-data-management.md` — IPFS artifacts, PostgreSQL metrics
