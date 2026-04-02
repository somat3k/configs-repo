---
name: model-d
component: model
model-id: model-d
target-module: defi
description: 'Default model configuration for model-d — the DeFi model. Covers sequence-aware LSTM/Transformer architecture, on-chain feature schema, strategy selection, multi-objective optimisation (yield/risk), confidence thresholding (≥0.75), and naming conventions used across the MLS project.'
---

# model-d — DeFi Model Configuration

> **Naming Convention**
> - Python class: `ModelD`
> - File prefix: `model_d_`
> - Enum value: `ModelType.DeFi` (`model-d`)
> - ONNX artifact: `artifacts/models/model_d_{version}.onnx`
> - JOBLIB artifact: `artifacts/models/model_d_{version}.joblib`
> - PostgreSQL `model_registry.model_name`: `"model-d"`
> - C# service: `IModelDInferenceService`

---

## Purpose

`model-d` optimises **DeFi strategy selection and position sizing** on HYPERLIQUID and fallback brokers. It is a sequence-aware model (LSTM encoder) that handles the temporal nature of on-chain data (funding rates, liquidity pool depth, gas prices). Confidence threshold: ≥ **0.75**. Acceleration level: **L2/L3** (multi-thread CPU or distributed inference for strategy sweeps).

**Critical rule enforced**: model-d never generates addresses, selects venues, or references protocols inline — all are loaded from the `blockchain_addresses` PostgreSQL table. **No Uniswap** references anywhere in this model.

---

## Feature Schema

### Sequence Features (time-series, window = 60 steps)

| # | Feature | Description | Normalisation |
|---|---------|-------------|---------------|
| 0 | `funding_rate` | Perpetual funding rate (HYPERLIQUID) | z-score |
| 1 | `open_interest_delta` | Change in open interest | z-score |
| 2 | `mark_price` | Mark price (HYPERLIQUID) | log-return |
| 3 | `liquidity_depth_bps` | Depth within ±50 bps | z-score |
| 4 | `liquidation_volume` | Liquidation volume in USD | log1p + z-score |

**Sequence shape**: `(batch, 60, 5)`

### Static Features (current snapshot)

| # | Feature | Description | Range |
|---|---------|-------------|-------|
| 0 | `portfolio_delta` | Current net delta exposure | ℝ z-score |
| 1 | `available_collateral_usd` | Free collateral | log1p z-score |
| 2 | `gas_price_gwei` | Current gas price | z-score |
| 3 | `market_regime` | Encoded regime (trending/ranging/volatile) | {0,1,2} one-hot |
| 4 | `hour_sin` | Cyclical hour (sin) | [-1, 1] |
| 5 | `hour_cos` | Cyclical hour (cos) | [-1, 1] |

**Static vector size**: `STATIC_DIM = 7` (including one-hot expansion)

---

## Architecture

```python
import torch
import torch.nn as nn
from dataclasses import dataclass, field


@dataclass
class ModelDConfig:
    """Default hyperparameters for model-d."""
    # Sequence encoder
    seq_len: int = 60
    seq_features: int = 5
    lstm_hidden: int = 128
    lstm_layers: int = 2
    lstm_dropout: float = 0.2

    # Static feature branch
    static_dim: int = 7
    static_hidden: int = 64

    # Fusion + decision layers
    fusion_hidden: int = 256
    decision_hidden: int = 128
    n_strategies: int = 6        # LONG / SHORT / NEUTRAL / HEDGE_LONG / HEDGE_SHORT / EXIT

    # Training
    batch_size: int = 256
    learning_rate: float = 3e-4
    weight_decay: float = 1e-4
    max_epochs: int = 400
    warmup_steps: int = 2000
    early_stopping_patience: int = 40
    gradient_clip: float = 0.5   # Tighter clip for LSTM stability

    # Confidence
    confidence_threshold: float = 0.75
    mc_dropout_samples: int = 25

    # Acceleration
    acceleration_level: str = "L2"   # Multi-thread for parallel strategy sweeps
    torch_num_threads: int = 4
    compile_mode: str = "reduce-overhead"


class ModelD(nn.Module):
    """DeFi strategy model — model-d.

    Outputs:
        strategy_logits: (B, 6)   — raw scores for 6 strategy types
        position_size: (B, 1)     — recommended position size as fraction of collateral ∈ [0, 1]
        confidence: (B, 1)        — prediction confidence ∈ [0, 1]
    """

    def __init__(self, cfg: ModelDConfig = ModelDConfig()):
        super().__init__()
        self.cfg = cfg

        # Sequence encoder: bidirectional LSTM
        self.lstm = nn.LSTM(
            input_size=cfg.seq_features,
            hidden_size=cfg.lstm_hidden,
            num_layers=cfg.lstm_layers,
            batch_first=True,
            dropout=cfg.lstm_dropout if cfg.lstm_layers > 1 else 0.0,
            bidirectional=True,
        )
        # Bidirectional doubles output size
        lstm_out_dim = cfg.lstm_hidden * 2

        # Static feature branch
        self.static_branch = nn.Sequential(
            nn.Linear(cfg.static_dim, cfg.static_hidden),
            nn.LayerNorm(cfg.static_hidden),
            nn.GELU(),
        )

        # Attention pooling over LSTM sequence output
        self.attn_pool = nn.Linear(lstm_out_dim, 1)

        # Fusion layer
        fusion_in = lstm_out_dim + cfg.static_hidden
        self.fusion = nn.Sequential(
            nn.Linear(fusion_in, cfg.fusion_hidden),
            nn.LayerNorm(cfg.fusion_hidden),
            nn.GELU(),
            nn.Dropout(0.2),
            nn.Linear(cfg.fusion_hidden, cfg.decision_hidden),
            nn.LayerNorm(cfg.decision_hidden),
            nn.GELU(),
        )

        # Output heads
        self.strategy_head = nn.Linear(cfg.decision_hidden, cfg.n_strategies)
        self.position_head = nn.Sequential(
            nn.Linear(cfg.decision_hidden, 1),
            nn.Sigmoid(),   # ∈ [0, 1] — fraction of available collateral
        )
        self.confidence_head = nn.Linear(cfg.decision_hidden, 1)

    def forward(self, seq: torch.Tensor, static: torch.Tensor):
        # Sequence encoding
        lstm_out, _ = self.lstm(seq)        # (B, T, lstm_hidden*2)

        # Attention pooling: weighted sum over time steps
        attn_w = torch.softmax(self.attn_pool(lstm_out), dim=1)  # (B, T, 1)
        seq_repr = (lstm_out * attn_w).sum(dim=1)                 # (B, lstm_hidden*2)

        # Static branch
        static_repr = self.static_branch(static)                  # (B, static_hidden)

        # Fuse and decode
        fused = torch.cat([seq_repr, static_repr], dim=-1)
        decision = self.fusion(fused)

        strategy_logits = self.strategy_head(decision)
        position_size = self.position_head(decision)
        confidence = torch.sigmoid(self.confidence_head(decision))

        return strategy_logits, position_size, confidence
```

---

## Training Notes

- **Loss function**:
  - `CrossEntropy(strategy_logits, strategy_label)` — strategy selection
  - `MSE(position_size, optimal_fraction)` — position sizing regression
  - `BCE(confidence, outcome_correct)` — confidence calibration
  - Total: `L = L_strategy + 0.5 * L_position + 0.1 * L_confidence`
- **Sequence normalisation**: Apply z-score normalisation per feature across the training window; store mean/std in `feature_store` for inference-time denormalisation
- **L2 acceleration**: `torch.set_num_threads(4)` with `DataLoader(num_workers=4)`; use `persistent_workers=True` for sequence loading
- **LSTM stability**: gradient clipping at 0.5 is tighter than model-t/model-a; monitor gradient norms during training

---

## Export Notes

```python
# model-d has two inputs — export with named inputs
dummy_seq = torch.zeros(1, ModelDConfig.seq_len, ModelDConfig.seq_features)
dummy_static = torch.zeros(1, ModelDConfig.static_dim)

torch.onnx.export(
    model, (dummy_seq, dummy_static),
    f"artifacts/models/model_d_{version}.onnx",
    input_names=["sequence_features", "static_features"],
    output_names=["strategy_logits", "position_size", "confidence"],
    dynamic_axes={
        "sequence_features": {0: "batch_size"},
        "static_features": {0: "batch_size"},
        "strategy_logits": {0: "batch_size"},
    },
    opset_version=17,
)
```

---

## C# Inference Interface

```csharp
/// <summary>Inference service for model-d (DeFi strategy model).</summary>
public interface IModelDInferenceService
{
    /// <summary>Select DeFi strategy — target &lt; 20ms (L2 acceleration, LSTM inference).</summary>
    Task<ModelDResult> SelectStrategyAsync(ModelDFeatures features, CancellationToken ct = default);
    /// <summary>Sweep N strategy scenarios in parallel.</summary>
    Task<IReadOnlyList<ModelDResult>> SweepScenariosAsync(IReadOnlyList<ModelDFeatures> scenarios, CancellationToken ct);
}

public record ModelDSequenceFeatures(
    float FundingRate, float OpenInterestDelta, float MarkPrice,
    float LiquidityDepthBps, float LiquidationVolume
);

public record ModelDFeatures(
    ModelDSequenceFeatures[] Sequence,    // Length = 60 (seq_len)
    float PortfolioDelta, float AvailableCollateralUsd, float GasPriceGwei,
    int MarketRegime, float HourSin, float HourCos
);

public enum DeFiStrategy { Long, Short, Neutral, HedgeLong, HedgeShort, Exit }

public record ModelDResult(
    DeFiStrategy Strategy,
    float PositionSizeFraction,  // Fraction of available collateral ∈ [0, 1]
    float Confidence,             // Reject if < 0.75
    float InferenceMs
);
```

---

## Naming Convention Summary

| Context | Convention | Example |
|---------|-----------|---------|
| Python class | `ModelD` | `class ModelD(nn.Module)` |
| Python config | `ModelDConfig` | `cfg = ModelDConfig()` |
| File prefix | `model_d_` | `model_d_v1.onnx` |
| Enum | `ModelType.DeFi` | `ModelType.DeFi` |
| C# interface | `IModelDInferenceService` | — |
| C# result | `ModelDResult` | — |
| DB `model_name` | `"model-d"` | `WHERE model_name = 'model-d'` |
| Module consumer | `defi` | `src/modules/defi/` |
| Strategy enum | `DeFiStrategy` | `DeFiStrategy.Long` |
