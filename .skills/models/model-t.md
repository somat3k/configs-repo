---
name: model-t
component: model
model-id: model-t
target-module: trader
description: 'Default model configuration for model-t — the Trading model. Covers architecture, hyperparameters, feature schema, training loop, ONNX/JOBLIB export, confidence thresholds, and naming conventions used across the MLS project.'
---

# model-t — Trading Model Configuration

> **Naming Convention**
> - Python class: `ModelT`
> - File prefix: `model_t_`
> - Enum value: `ModelType.Trading` (`model-t`)
> - ONNX artifact: `artifacts/models/model_t_{version}.onnx`
> - JOBLIB artifact: `artifacts/models/model_t_{version}.joblib`
> - PostgreSQL `model_registry.model_name`: `"model-t"`
> - C# service: `IModelTInferenceService`

---

## Purpose

`model-t` generates **trading signals** (BUY / SELL / HOLD) with a calibrated confidence score. It is the primary ML model consumed by the `trader` module. The model must produce a decision in **< 10ms** (L1 acceleration, single-thread vectorised inference).

---

## Feature Schema

| # | Feature | Description | Range |
|---|---------|-------------|-------|
| 0 | `rsi_14` | 14-period RSI | [0, 1] (normalised) |
| 1 | `macd_signal` | MACD − signal line | ℝ (z-score normalised) |
| 2 | `bb_position` | Price position within Bollinger Bands | [0, 1] |
| 3 | `volume_delta` | Relative volume vs 20-period avg | ℝ (z-score) |
| 4 | `momentum_20` | 20-period price momentum | ℝ (z-score) |
| 5 | `atr_14` | Average True Range (volatility proxy) | ℝ (z-score) |
| 6 | `spread_bps` | Bid-ask spread in basis points | [0, ∞) |
| 7 | `vwap_distance` | Distance from VWAP (normalised) | ℝ (z-score) |

**Feature vector size**: `INPUT_DIM = 8`

---

## Architecture

```python
import torch
import torch.nn as nn
from dataclasses import dataclass


@dataclass
class ModelTConfig:
    """Default hyperparameters for model-t."""
    # Architecture
    input_dim: int = 8           # Feature vector size (fixed)
    hidden_dims: tuple = (128, 64, 32)  # Depth-3 MLP
    n_classes: int = 3           # BUY=0, SELL=1, HOLD=2
    dropout_rate: float = 0.2
    use_layer_norm: bool = True
    use_residual: bool = False   # Depth-3 doesn't need residual

    # Training
    batch_size: int = 512
    learning_rate: float = 1e-3
    weight_decay: float = 1e-5
    max_epochs: int = 200
    warmup_steps: int = 500
    early_stopping_patience: int = 20

    # Confidence
    confidence_threshold: float = 0.70   # Reject signals below this
    mc_dropout_samples: int = 20          # Monte Carlo dropout samples

    # Acceleration
    acceleration_level: str = "L1"        # Single-thread vectorised (< 10ms target)
    torch_num_threads: int = 1
    compile_mode: str = "reduce-overhead"


class ModelT(nn.Module):
    """Trading signal model — model-t.

    Outputs:
        logits: (B, 3)   — raw class scores [BUY, SELL, HOLD]
        confidence: (B, 1) — calibrated confidence ∈ [0, 1]
    """

    def __init__(self, cfg: ModelTConfig = ModelTConfig()):
        super().__init__()
        self.cfg = cfg

        dims = [cfg.input_dim] + list(cfg.hidden_dims)
        layers = []
        for i in range(len(dims) - 1):
            layers += [
                nn.Linear(dims[i], dims[i + 1]),
                nn.LayerNorm(dims[i + 1]) if cfg.use_layer_norm else nn.Identity(),
                nn.GELU(),
                nn.Dropout(cfg.dropout_rate),
            ]
        self.backbone = nn.Sequential(*layers)
        self.classifier = nn.Linear(dims[-1], cfg.n_classes)
        self.confidence_head = nn.Linear(dims[-1], 1)

    def forward(self, x: torch.Tensor):
        features = self.backbone(x)
        logits = self.classifier(features)
        confidence = torch.sigmoid(self.confidence_head(features))
        return logits, confidence
```

---

## Training Loop

```python
import torch
import torch.nn.functional as F
from torch.optim import AdamW
from pathlib import Path


def train_model_t(
    cfg: ModelTConfig,
    X_train: torch.Tensor,
    y_train: torch.Tensor,
    X_val: torch.Tensor,
    y_val: torch.Tensor,
    output_dir: Path = Path("artifacts/models"),
) -> ModelT:
    """Train model-t and export ONNX + JOBLIB artifacts."""
    import torch, math
    from torch.optim.lr_scheduler import LambdaLR

    # Apply L1 acceleration
    torch.set_num_threads(cfg.torch_num_threads)

    model = ModelT(cfg)
    optimizer = AdamW(model.parameters(), lr=cfg.learning_rate, weight_decay=cfg.weight_decay)

    # Cosine warmup schedule (see artificial-intelligence.md §3)
    total_steps = cfg.max_epochs * (len(X_train) // cfg.batch_size + 1)
    scheduler = LambdaLR(optimizer, lambda s: min(
        s / max(1, cfg.warmup_steps),
        0.5 * (1 + math.cos(math.pi * max(0, s - cfg.warmup_steps) / (total_steps - cfg.warmup_steps)))
    ))

    best_val_loss = float("inf")
    patience_counter = 0

    for epoch in range(cfg.max_epochs):
        model.train()
        idx = torch.randperm(len(X_train))
        epoch_loss = 0.0
        for start in range(0, len(X_train), cfg.batch_size):
            batch_idx = idx[start: start + cfg.batch_size]
            logits, conf = model(X_train[batch_idx])
            # Classification loss
            cls_loss = F.cross_entropy(logits, y_train[batch_idx])
            # Confidence regularisation: penalise overconfidence on wrong predictions
            pred = logits.argmax(-1)
            correct = (pred == y_train[batch_idx]).float().unsqueeze(-1)
            conf_loss = F.binary_cross_entropy(conf, correct)
            loss = cls_loss + 0.1 * conf_loss

            optimizer.zero_grad(set_to_none=True)
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
            scheduler.step()
            epoch_loss += loss.item()

        # Validation
        model.eval()
        with torch.no_grad():
            val_logits, _ = model(X_val)
            val_loss = F.cross_entropy(val_logits, y_val).item()

        if val_loss < best_val_loss:
            best_val_loss = val_loss
            patience_counter = 0
            torch.save(model.state_dict(), output_dir / "model_t_best.pt")
        else:
            patience_counter += 1
            if patience_counter >= cfg.early_stopping_patience:
                break

    model.load_state_dict(torch.load(output_dir / "model_t_best.pt"))
    return model
```

---

## Export — ONNX + JOBLIB

```python
import torch
import joblib
from skl2onnx.helpers.onnx_helper import save_onnx_model


def export_model_t(model: ModelT, version: str, output_dir: Path):
    """Export model-t to ONNX and JOBLIB. Store CID in model_registry."""
    model.eval()

    # ONNX export (used by C# ONNX Runtime for < 10ms inference)
    dummy = torch.zeros(1, ModelTConfig.input_dim)
    onnx_path = output_dir / f"model_t_{version}.onnx"
    torch.onnx.export(
        model, dummy, onnx_path,
        input_names=["input_features"],
        output_names=["logits", "confidence"],
        dynamic_axes={"input_features": {0: "batch_size"}, "logits": {0: "batch_size"}},
        opset_version=17,
    )

    # JOBLIB export (Python fallback + scikit-learn pipeline interop)
    joblib_path = output_dir / f"model_t_{version}.joblib"
    joblib.dump({"model_state": model.state_dict(), "config": model.cfg}, joblib_path, compress=3)

    print(f"[model-t] Exported ONNX → {onnx_path}")
    print(f"[model-t] Exported JOBLIB → {joblib_path}")
    return onnx_path, joblib_path
```

---

## C# Inference Interface

```csharp
/// <summary>Inference service for model-t (trading signal model).</summary>
public interface IModelTInferenceService
{
    /// <summary>Run synchronous inference — target &lt; 10ms (L1 acceleration).</summary>
    Task<ModelTResult> PredictAsync(ModelTFeatures features, CancellationToken ct = default);
    /// <summary>Stream inference results for a market data feed.</summary>
    IAsyncEnumerable<ModelTResult> StreamAsync(IAsyncEnumerable<ModelTFeatures> feed, CancellationToken ct);
}

public record ModelTFeatures(
    float Rsi14, float MacdSignal, float BbPosition, float VolumeDelta,
    float Momentum20, float Atr14, float SpreadBps, float VwapDistance
);

public record ModelTResult(
    TradeSide Signal,      // BUY | SELL | HOLD
    float Confidence,      // ∈ [0, 1] — reject if < ModelTConfig.ConfidenceThreshold (0.70)
    float[] Probabilities, // [P(BUY), P(SELL), P(HOLD)]
    float InferenceMs      // Latency tracking
);
```

---

## Naming Convention Summary

| Context | Convention | Example |
|---------|-----------|---------|
| Python class | `ModelT` | `class ModelT(nn.Module)` |
| Python config | `ModelTConfig` | `cfg = ModelTConfig()` |
| File prefix | `model_t_` | `model_t_v1.onnx` |
| Enum | `ModelType.Trading` | `ModelType.Trading` |
| C# interface | `IModelTInferenceService` | — |
| C# result | `ModelTResult` | — |
| DB `model_name` | `"model-t"` | `WHERE model_name = 'model-t'` |
| Module consumer | `trader` | `src/modules/trader/` |
