---
name: artificial-intelligence
source: github/awesome-copilot/skills/semantic-kernel + custom
description: 'AI integration patterns using Microsoft Semantic Kernel, ONNX inference, neural network architecture, and AI-driven trading signal generation for the MLS platform.'
---

# Artificial Intelligence — MLS Trading Platform

## Semantic Kernel Integration
Use `Microsoft.SemanticKernel` for AI orchestration:
```csharp
var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o", apiKey)
    .Build();

kernel.Plugins.AddFromType<TradingAnalysisPlugin>();
kernel.Plugins.AddFromType<MarketDataPlugin>();
```

## ONNX Runtime Inference
```csharp
// High-performance ONNX inference
using var session = new InferenceSession(
    modelPath,
    new SessionOptions { ExecutionMode = ExecutionMode.ORT_PARALLEL }
);

var inputMeta = session.InputMetadata;
var container = new List<NamedOnnxValue>();
container.Add(NamedOnnxValue.CreateFromTensor("input_features", featureTensor));

using var results = session.Run(container);
var prediction = results[0].AsEnumerable<float>().First();
```

## AI Plugin Architecture
Each trading module can expose Semantic Kernel plugins:
- `TradingAnalysisPlugin` — analyze market conditions
- `RiskAssessmentPlugin` — evaluate position risk
- `ArbitrageDetectorPlugin` — scan for opportunities
- `DeFiStrategyPlugin` — optimize DeFi positions

## Neural Network Architecture Patterns
- **Signal Network**: [Features] → Normalization → [128, ReLU] → [64, ReLU] → Dropout(0.3) → [1, Sigmoid]
- **Regime Network**: [OHLCV Features] → LSTM(hidden=128) → [64, ReLU] → [N_Regimes, Softmax]
- **Arbitrage Scorer**: [Spread, Volume, Liquidity, Time] → [64, ReLU] → [32, ReLU] → [1, Sigmoid]

## Adam Optimizer Configuration
```python
import torch.optim as optim
optimizer = optim.Adam(
    model.parameters(),
    lr=1e-3,         # Learning rate
    betas=(0.9, 0.999),  # Momentum terms
    eps=1e-8,        # Numerical stability
    weight_decay=1e-5  # L2 regularization
)
```

## Inference Pipeline
1. Raw market features received via WebSocket
2. Feature vector assembled by ML-Runtime module
3. ONNX model runs inference (< 10ms)
4. Result envelope published to Block Controller
5. Block Controller routes signal to appropriate trading module

## AI Safety & Monitoring
- All AI predictions logged with confidence scores
- Implement confidence thresholding (reject low-confidence signals)
- Track model drift via PostgreSQL `model_metrics` table
- Alert on anomalous prediction distributions
