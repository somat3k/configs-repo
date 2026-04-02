---
name: model-a
component: model
model-id: model-a
target-module: arbitrager
description: 'Default model configuration for model-a — the Arbitrage model. Covers multi-input architecture, spread/liquidity feature schema, opportunity scoring, high-confidence thresholding (≥0.80), and naming conventions used across the MLS project.'
---

# model-a — Arbitrage Model Configuration

> **Naming Convention**
> - Python class: `ModelA`
> - File prefix: `model_a_`
> - Enum value: `ModelType.Arbitrage` (`model-a`)
> - ONNX artifact: `artifacts/models/model_a_{version}.onnx`
> - JOBLIB artifact: `artifacts/models/model_a_{version}.joblib`
> - PostgreSQL `model_registry.model_name`: `"model-a"`
> - C# service: `IModelAInferenceService`

---

## Purpose

`model-a` scores **arbitrage opportunities** across exchanges, outputting a viability score ∈ [0, 1] and an expected profit estimate. Consumed by the `arbitrager` module. Higher confidence threshold than `model-t` (≥ **0.80**) because execution is capital-intensive. Uses **L2 acceleration** (multi-thread CPU) since multiple opportunities are scanned in parallel.

---

## Feature Schema

| # | Feature | Description | Range |
|---|---------|-------------|-------|
| 0 | `spread_bps` | Price spread between venues in basis points | [0, ∞) |
| 1 | `buy_liquidity_usd` | Available liquidity on buy side (USD) | [0, ∞) z-score |
| 2 | `sell_liquidity_usd` | Available liquidity on sell side (USD) | [0, ∞) z-score |
| 3 | `fee_total_bps` | Total round-trip fee (both legs) in bps | [0, ∞) |
| 4 | `execution_lag_ms` | Estimated execution lag in ms | [0, ∞) z-score |
| 5 | `volatility_1m` | 1-minute realised volatility | ℝ z-score |
| 6 | `net_spread_bps` | `spread_bps - fee_total_bps` (raw alpha) | ℝ |
| 7 | `venue_pair_id` | Encoded venue pair (embedding lookup) | int → embedding |
| 8 | `hour_sin` | Cyclical hour encoding (sin) | [-1, 1] |
| 9 | `hour_cos` | Cyclical hour encoding (cos) | [-1, 1] |

**Feature vector size**: `INPUT_DIM = 10` (after venue embedding concatenation: `INPUT_DIM_TOTAL = 18`)

---

## Architecture

```python
import torch
import torch.nn as nn
from dataclasses import dataclass


@dataclass
class ModelAConfig:
    """Default hyperparameters for model-a."""
    # Architecture
    input_dim: int = 10             # Raw features (venue embedding added separately)
    venue_vocab_size: int = 32      # Number of known venue pairs
    venue_embedding_dim: int = 8    # Embedding dimension for venue pair
    hidden_dims: tuple = (256, 128, 64, 32)  # Depth-4 MLP for richer scoring
    dropout_rate: float = 0.25
    use_layer_norm: bool = True
    use_residual: bool = True        # Residual connections at depth >= 4

    # Training
    batch_size: int = 1024           # Large batch for high-throughput opportunity log
    learning_rate: float = 5e-4
    weight_decay: float = 1e-4
    max_epochs: int = 300
    warmup_steps: int = 1000
    early_stopping_patience: int = 30
    label_smoothing: float = 0.05   # Smooth labels — market noise tolerance

    # Confidence
    confidence_threshold: float = 0.80   # Higher bar than model-t
    mc_dropout_samples: int = 30          # More MC samples — capital at risk

    # Acceleration
    acceleration_level: str = "L2"        # Multi-thread: scanning N opportunities in parallel
    torch_num_threads: int = 4
    compile_mode: str = "reduce-overhead"


class ResidualBlock(nn.Module):
    def __init__(self, dim: int, dropout: float):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(dim, dim), nn.LayerNorm(dim), nn.GELU(), nn.Dropout(dropout),
            nn.Linear(dim, dim), nn.LayerNorm(dim),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return x + self.net(x)  # residual skip connection


class ModelA(nn.Module):
    """Arbitrage opportunity scoring model — model-a.

    Outputs:
        score: (B, 1)      — opportunity viability ∈ [0, 1]
        profit_est: (B, 1) — estimated profit in bps (regression head)
        confidence: (B, 1) — prediction confidence ∈ [0, 1]
    """

    def __init__(self, cfg: ModelAConfig = ModelAConfig()):
        super().__init__()
        self.cfg = cfg

        # Venue pair embedding (learnable)
        self.venue_emb = nn.Embedding(cfg.venue_vocab_size, cfg.venue_embedding_dim)

        # Input projection
        total_input = cfg.input_dim + cfg.venue_embedding_dim
        self.input_proj = nn.Linear(total_input, cfg.hidden_dims[0])

        # Depth-4 backbone with residual blocks
        self.layers = nn.ModuleList()
        for i in range(len(cfg.hidden_dims) - 1):
            self.layers.append(ResidualBlock(cfg.hidden_dims[i], cfg.dropout_rate))
            if cfg.hidden_dims[i] != cfg.hidden_dims[i + 1]:
                self.layers.append(nn.Linear(cfg.hidden_dims[i], cfg.hidden_dims[i + 1]))

        final_dim = cfg.hidden_dims[-1]
        self.score_head = nn.Linear(final_dim, 1)       # viability score
        self.profit_head = nn.Linear(final_dim, 1)      # profit estimate (bps)
        self.confidence_head = nn.Linear(final_dim, 1)  # confidence

    def forward(self, x: torch.Tensor, venue_ids: torch.Tensor):
        # Concatenate numerical features with venue embedding
        emb = self.venue_emb(venue_ids)
        x = torch.cat([x, emb], dim=-1)
        x = self.input_proj(x)
        for layer in self.layers:
            x = layer(x)
        score = torch.sigmoid(self.score_head(x))
        profit_est = self.profit_head(x)           # unbounded regression
        confidence = torch.sigmoid(self.confidence_head(x))
        return score, profit_est, confidence
```

---

## Training Notes

- **Loss function**: `BCE(score, is_profitable) + MSE(profit_est, actual_profit_bps) + 0.1 * conf_loss`
- **Class imbalance**: profitable opportunities are rare (~5-15% of samples); use weighted sampling or `pos_weight` in `BCEWithLogitsLoss`
- **Label smoothing**: `label_smoothing=0.05` applied to binary labels to account for market noise
- **Acceleration L2**: Use `torch.set_num_threads(4)` and DataLoader `num_workers=4` for parallel batch construction from streaming opportunity log

---

## C# Inference Interface

```csharp
/// <summary>Inference service for model-a (arbitrage scoring model).</summary>
public interface IModelAInferenceService
{
    /// <summary>Score a single arbitrage opportunity — target &lt; 5ms (L2 acceleration).</summary>
    Task<ModelAResult> ScoreAsync(ModelAFeatures features, CancellationToken ct = default);
    /// <summary>Batch-score multiple opportunities in parallel.</summary>
    Task<IReadOnlyList<ModelAResult>> ScoreBatchAsync(IReadOnlyList<ModelAFeatures> batch, CancellationToken ct);
}

public record ModelAFeatures(
    float SpreadBps, float BuyLiquidityUsd, float SellLiquidityUsd,
    float FeeTotalBps, float ExecutionLagMs, float Volatility1M,
    float NetSpreadBps, int VenuePairId, float HourSin, float HourCos
);

public record ModelAResult(
    float ViabilityScore,    // ∈ [0, 1] — reject if < ModelAConfig.ConfidenceThreshold (0.80)
    float EstimatedProfitBps, // Regression estimate
    float Confidence,         // Prediction confidence
    float InferenceMs
);
```

---

## Naming Convention Summary

| Context | Convention | Example |
|---------|-----------|---------|
| Python class | `ModelA` | `class ModelA(nn.Module)` |
| Python config | `ModelAConfig` | `cfg = ModelAConfig()` |
| File prefix | `model_a_` | `model_a_v1.onnx` |
| Enum | `ModelType.Arbitrage` | `ModelType.Arbitrage` |
| C# interface | `IModelAInferenceService` | — |
| C# result | `ModelAResult` | — |
| DB `model_name` | `"model-a"` | `WHERE model_name = 'model-a'` |
| Module consumer | `arbitrager` | `src/modules/arbitrager/` |
