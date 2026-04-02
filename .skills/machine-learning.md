---
name: machine-learning
source: custom (MLS Trading Platform) + semantic-kernel
description: 'Machine learning model development, ONNX/JOBLIB export, Neural Networks with Adam optimizer, FastForest classification, runtime inference, and ML pipeline architecture.'
---

# Machine Learning — MLS Trading Platform

## ML Runtime Architecture
The `ml-runtime` module is a hybrid C#/Python service:
- **Python** handles model training (scikit-learn, PyTorch, XGBoost)
- **C# ONNX Runtime** handles production inference (low latency)
- Models exported as both `.onnx` (C# inference) and `.joblib` (Python fallback)
- Runtime inference served via WebSocket streaming and HTTP REST endpoints

## Neural Network Design
- Use **Adam optimizer** for all neural network training
- Implement custom `AdamEquations` wrapper in Python training scripts
- Architecture: Input → Normalization → Dense layers → Dropout → Output
- Support **FastForest** (gradient-boosted trees) for classification tasks
- Use **ONNX** as the universal model format for C# runtime inference

## Feature Engineering
- All external data pre-defined in typed `FeatureSchema` classes
- Features stored in PostgreSQL `feature_store` table with versioning
- IPFS used for large feature matrix storage and model artifacts
- Support real-time feature computation from streaming market data

## Training Pipeline
1. Raw data ingested by Data-Driven Layer
2. Features computed by Transformational Systems Module
3. Feature sets indexed by Data Indexing Module
4. Model trained by ML-Runtime Python service
5. Model validated and exported to ONNX + JOBLIB
6. Model registered in Model Registry
7. Inference available via ML-Runtime HTTP/WebSocket API

## C# ONNX Inference
```csharp
// Use Microsoft.ML.OnnxRuntime
using var session = new InferenceSession("model.onnx");
var inputTensor = new DenseTensor<float>(features, new[] { 1, featureCount });
var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", inputTensor) };
using var results = session.Run(inputs);
var prediction = results.First().AsEnumerable<float>().ToArray();
```

## JOBLIB Python Export
```python
import joblib
# Save
joblib.dump(model, "model.joblib", compress=3)
# Load
model = joblib.load("model.joblib")
```

## ML Model Types
- **Price Prediction**: LSTM/Transformer for sequence modeling
- **Signal Classification**: FastForest binary classifier (buy/sell/hold)
- **Regime Detection**: Hidden Markov Model for market regime identification
- **Arbitrage Scoring**: Multi-input neural network for opportunity scoring
- **Risk Assessment**: Ensemble model combining volatility and correlation features

## Performance Requirements
- Inference latency: < 10ms for trade signals
- Throughput: > 1000 inferences/second
- Model update cadence: Configurable, minimum 1 hour
- Support A/B testing between model versions
