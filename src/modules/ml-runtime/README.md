# ML Runtime Module — Machine Learning Training & Inference

## Overview
The ML Runtime module handles model training (Python) and production inference (C# ONNX).

## Languages
- **Python 3.12** — training, feature engineering, model export
- **C# .NET 9** — inference server, model management, WebSocket streaming

## Model Types
| Model | Algorithm | Export |
|-------|-----------|--------|
| trader-signal | FastForest / Neural Net | ONNX + JOBLIB |
| arbitrage-scorer | Gradient Boosted Trees | ONNX + JOBLIB |
| defi-strategy | LSTM / Transformer | ONNX + JOBLIB |
| regime-detector | Hidden Markov Model | JOBLIB |

## Ports: HTTP 5600 / WebSocket 6600
## Session prompt: [docs/SESSION.md](docs/SESSION.md)
