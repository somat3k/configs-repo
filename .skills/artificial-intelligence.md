---
name: artificial-intelligence
source: github/awesome-copilot/skills/semantic-kernel + pytorch-docs + custom
description: 'Advanced AI model engineering for MLS — torch parameter multiplication, multi-depth hyperparameter dynamics, L1/L2/L3/L4 process acceleration, and decision-making confidence architecture. Two separate components: acceleration/ (system) and models/ (model-t, model-a, model-d).'
---

# Artificial Intelligence — MLS Trading Platform

> **Component Split**: This skill has two specialised sub-directories:
> - **[`.skills/acceleration/`](acceleration/)** — System-level L1/L2/L3/L4 process acceleration and CPU thread control
> - **[`.skills/models/`](models/)** — Default model configurations: `model-t` (trading), `model-a` (arbitrage), `model-d` (defi)

---

## 1. Torch Parameter Multiplication

Use `torch.nn.Parameter` for learnable tensor multiplication that participates in backprop. This is the foundation for attention heads, gating mechanisms, and mixture-of-experts layers in the trading models.

```python
import torch
import torch.nn as nn
import torch.nn.functional as F


class ParameterMultiplication(nn.Module):
    """Learnable element-wise and matrix multiplication blocks.

    Naming convention:
        model-t  — TradingMultiplicationBlock
        model-a  — ArbitrageMultiplicationBlock
        model-d  — DeFiMultiplicationBlock
    """

    def __init__(self, input_dim: int, output_dim: int, n_heads: int = 4):
        super().__init__()
        # Learnable scaling parameter (multiplicative bias)
        self.scale = nn.Parameter(torch.ones(input_dim))
        # Learnable shift parameter (additive bias)
        self.shift = nn.Parameter(torch.zeros(input_dim))
        # Multi-head projection weights
        self.W_q = nn.Parameter(torch.randn(n_heads, input_dim, output_dim // n_heads) * 0.02)
        self.W_k = nn.Parameter(torch.randn(n_heads, input_dim, output_dim // n_heads) * 0.02)
        self.W_v = nn.Parameter(torch.randn(n_heads, input_dim, output_dim // n_heads) * 0.02)
        self.n_heads = n_heads
        self.output_dim = output_dim

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        # Multiplicative feature scaling: element-wise parameter multiplication
        x = x * self.scale + self.shift  # (batch, input_dim)

        # Multi-head parameter multiplication
        B = x.size(0)
        q = torch.einsum("bi,hio->bho", x, self.W_q)  # (B, heads, head_dim)
        k = torch.einsum("bi,hio->bho", x, self.W_k)
        v = torch.einsum("bi,hio->bho", x, self.W_v)

        # Scaled dot-product attention (parameter multiplication core)
        scale = (self.output_dim // self.n_heads) ** -0.5
        attn = torch.einsum("bhi,bhj->bhij", q, k) * scale
        attn = F.softmax(attn, dim=-1)
        out = torch.einsum("bhij,bhj->bhi", attn, v)
        return out.reshape(B, -1)  # (B, output_dim)
```

---

## 2. Multi-Depth Neural Architecture

All MLS models support configurable depth levels. Deeper architectures are used for slower, higher-confidence decisions (regime detection); shallower for ultra-low-latency inference (trade signals).

```python
from dataclasses import dataclass, field
from typing import Literal


@dataclass
class DepthConfig:
    """Controls architecture depth and hyperparameter scaling per level."""

    depth: Literal[1, 2, 3, 4, 5, 6] = 3
    # Layer widths — automatically scaled by depth
    base_hidden: int = 128
    width_multiplier: float = 1.5   # width grows with depth
    dropout_schedule: list[float] = field(default_factory=lambda: [0.1, 0.2, 0.3, 0.3, 0.2, 0.1])
    use_residual: bool = True       # residual connections for depth >= 3
    use_layer_norm: bool = True


def build_depth_layers(config: DepthConfig, input_dim: int, output_dim: int) -> nn.Sequential:
    """Build a variable-depth MLP with residual connections and layer norm."""
    layers = []
    current_dim = input_dim
    for i in range(config.depth):
        hidden = int(config.base_hidden * (config.width_multiplier ** i))
        layers += [
            nn.Linear(current_dim, hidden),
            nn.LayerNorm(hidden) if config.use_layer_norm else nn.Identity(),
            nn.GELU(),
            nn.Dropout(config.dropout_schedule[min(i, len(config.dropout_schedule) - 1)]),
        ]
        current_dim = hidden
    layers.append(nn.Linear(current_dim, output_dim))
    return nn.Sequential(*layers)
```

---

## 3. Higher-Level Hyperparameter Dynamics

Dynamic hyperparameter schedules improve convergence and generalisation across market regimes. Use these patterns in all model-t / model-a / model-d training loops.

```python
import math
from torch.optim.lr_scheduler import LambdaLR


def cosine_warmup_schedule(optimizer, warmup_steps: int, total_steps: int):
    """Warmup + cosine annealing — robust for financial time-series."""
    def lr_lambda(step: int) -> float:
        if step < warmup_steps:
            return step / max(1, warmup_steps)
        progress = (step - warmup_steps) / max(1, total_steps - warmup_steps)
        return 0.5 * (1.0 + math.cos(math.pi * progress))
    return LambdaLR(optimizer, lr_lambda)


class DynamicHyperparameterScheduler:
    """Adapts learning rate, batch size, and regularisation during training."""

    def __init__(self, base_lr: float = 1e-3, base_batch: int = 256):
        self.base_lr = base_lr
        self.base_batch = base_batch
        self._loss_history: list[float] = []

    def step(self, val_loss: float) -> dict:
        self._loss_history.append(val_loss)
        if len(self._loss_history) < 5:
            return {"lr": self.base_lr, "batch_size": self.base_batch}

        recent = self._loss_history[-5:]
        improving = recent[-1] < min(recent[:-1])

        return {
            # Scale LR up when improving, decay when plateauing
            "lr": self.base_lr * (1.05 if improving else 0.9),
            # Increase batch size over time for stability (linear scaling rule)
            "batch_size": min(self.base_batch * (1 + len(self._loss_history) // 20), 4096),
            # L2 regularisation — tighten when loss plateaus
            "weight_decay": 1e-5 if improving else 5e-5,
        }
```

---

## 4. Adam Optimizer — Full Configuration

```python
import torch.optim as optim

optimizer = optim.Adam(
    model.parameters(),
    lr=1e-3,            # Base learning rate (overridden by scheduler)
    betas=(0.9, 0.999), # Momentum terms: β1 for gradient, β2 for squared gradient
    eps=1e-8,           # Numerical stability (avoid division by zero)
    weight_decay=1e-5,  # L2 regularisation coefficient
    amsgrad=False,      # Set True for non-stationary financial data if Adam diverges
    foreach=True,       # Vectorised parameter update loop — ~20% faster on CPU
    maximize=False,
)

# AdamW (decoupled weight decay) — preferred for Transformer-based models
optimizer_w = optim.AdamW(
    model.parameters(),
    lr=1e-3,
    betas=(0.9, 0.999),
    eps=1e-8,
    weight_decay=1e-2,  # Higher WD for AdamW (decoupled from gradient)
    fused=True,         # CUDA-fused kernel — use when GPU available
)
```

---

## 5. Decision-Making Confidence Architecture

All MLS models output a confidence score alongside the primary prediction. This enables downstream modules to threshold and filter signals.

```python
class ConfidenceHead(nn.Module):
    """Dual-head output: prediction + calibrated confidence.

    Implements Monte Carlo Dropout for epistemic uncertainty estimation.
    """

    def __init__(self, hidden_dim: int, n_classes: int, mc_samples: int = 20):
        super().__init__()
        self.prediction_head = nn.Linear(hidden_dim, n_classes)
        self.confidence_head = nn.Linear(hidden_dim, 1)
        self.dropout = nn.Dropout(p=0.1)
        self.mc_samples = mc_samples

    def forward(self, x: torch.Tensor, training: bool = False):
        if training or self.mc_samples <= 1:
            logits = self.prediction_head(x)
            raw_conf = self.confidence_head(x)
            return logits, torch.sigmoid(raw_conf)

        # MC Dropout: run N forward passes with dropout active for uncertainty
        self.train()
        logit_samples = torch.stack(
            [self.prediction_head(self.dropout(x)) for _ in range(self.mc_samples)], dim=0
        )
        self.eval()
        mean_logits = logit_samples.mean(0)
        # Uncertainty = variance across samples (high variance → low confidence)
        uncertainty = logit_samples.var(0).mean(-1, keepdim=True)
        confidence = torch.sigmoid(-uncertainty * 10 + 5)  # normalise to [0, 1]
        return mean_logits, confidence
```

---

## 6. ONNX Runtime Inference (C#)

```csharp
// High-performance ONNX inference with parallel execution
using var session = new InferenceSession(
    modelPath,
    new SessionOptions
    {
        ExecutionMode = ExecutionMode.ORT_PARALLEL,
        InterOpNumThreads = Environment.ProcessorCount,
        IntraOpNumThreads = Environment.ProcessorCount / 2,
        GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
    }
);

var container = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input_features", featureTensor),
};

using var results = session.Run(container);
float prediction = results[0].AsEnumerable<float>().First();
float confidence = results[1].AsEnumerable<float>().First(); // confidence head output
```

---

## 7. Semantic Kernel Integration

```csharp
var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o", apiKey)
    .Build();

kernel.Plugins.AddFromType<TradingAnalysisPlugin>();   // model-t signals
kernel.Plugins.AddFromType<ArbitrageDetectorPlugin>(); // model-a opportunities
kernel.Plugins.AddFromType<DeFiStrategyPlugin>();      // model-d positions
kernel.Plugins.AddFromType<RiskAssessmentPlugin>();    // cross-model risk
```

---

## 8. AI Safety & Monitoring

- All AI predictions logged with confidence scores to `model_metrics` PostgreSQL table
- Confidence threshold per model: model-t ≥ 0.70, model-a ≥ 0.80, model-d ≥ 0.75
- Track model drift: monitor rolling prediction distribution vs. training distribution
- Alert on anomalous prediction distributions (KL divergence > threshold)
- A/B test new model versions before full rollout

---

## Component Index

| Component | Path | Purpose |
|-----------|------|---------|
| System Acceleration | [`.skills/acceleration/`](acceleration/) | L1/L2/L3/L4 CPU/thread acceleration for the platform |
| Trading Model | [`.skills/models/model-t.md`](models/model-t.md) | Default configuration for `model-t` (trader) |
| Arbitrage Model | [`.skills/models/model-a.md`](models/model-a.md) | Default configuration for `model-a` (arbitrager) |
| DeFi Model | [`.skills/models/model-d.md`](models/model-d.md) | Default configuration for `model-d` (defi) |
