---
name: python
source: github/awesome-copilot/skills/pytest-coverage + custom
description: 'Python development guidelines for ML training scripts, data processing pipelines, and inference services within the MLS platform.'
---

# Python Development — MLS Trading Platform

## Project Context
Python is used exclusively for:
1. ML model training scripts (`src/modules/ml-runtime/training/`)
2. Data processing pipelines (`src/modules/ml-runtime/pipelines/`)
3. IPFS interaction utilities (`src/modules/ml-runtime/storage/`)
4. Backtesting harnesses

## Environment Setup
- Use Python 3.12+
- Manage dependencies with `pyproject.toml` + `uv` or `pip-tools`
- Virtual environment in `.venv/` (gitignored)
- All ML deps pinned in `requirements.lock`

## Key Libraries
- `scikit-learn` — classical ML, FastForest, preprocessing
- `torch` — neural networks, Adam optimizer
- `onnx` + `skl2onnx` — model export
- `joblib` — model serialization
- `pandas` + `polars` — data manipulation
- `numpy` — numerical computation
- `aiohttp` — async HTTP for API calls
- `websockets` — WebSocket client for market data
- `pytest` + `pytest-cov` — testing

## Code Style
- Use type hints everywhere: `def train(features: np.ndarray, labels: np.ndarray) -> sklearn.base.BaseEstimator:`
- Use dataclasses or Pydantic models for structured data
- Follow PEP 8, enforced with `ruff`
- Docstrings in Google style

## Testing
- Use `pytest` with `pytest-cov` for coverage reports
- Target >80% coverage for all training and inference code
- Use `pytest-benchmark` for inference performance tests
- Mock external API calls in tests

## ML Training Pattern
```python
from dataclasses import dataclass
from sklearn.ensemble import GradientBoostingClassifier
import joblib, onnx
from skl2onnx import convert_sklearn

@dataclass
class TrainingConfig:
    model_name: str
    feature_count: int
    n_estimators: int = 100
    learning_rate: float = 0.01

def train_and_export(config: TrainingConfig, X_train, y_train) -> None:
    model = GradientBoostingClassifier(
        n_estimators=config.n_estimators,
        learning_rate=config.learning_rate
    )
    model.fit(X_train, y_train)
    joblib.dump(model, f"artifacts/{config.model_name}.joblib")
    # Export to ONNX
    onnx_model = convert_sklearn(model, "input", [("input", FloatTensorType([None, config.feature_count]))])
    with open(f"artifacts/{config.model_name}.onnx", "wb") as f:
        f.write(onnx_model.SerializeToString())
```
