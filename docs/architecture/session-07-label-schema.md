> ✅ **Status: Complete** — Implemented and verified in session 23 (workflow-demo).

# Session 07 — Multi-Dimensional Label Schema for Arbitrage Navigation

> **Reference**: [Designer Block Graph](designer-block-graph.md) | [Session Schedule](../session-schedule.md) (Session 07)
> **Skills**: `.skills/machine-learning.md` · `.skills/models/model-a.md` · `.skills/web3.md`

---

## Overview

The current `TrainSplitBlock` (Session 06) produces a **1-D integer label vector** —
a single class index per sample.  This is sufficient for basic classification tasks
(BUY/SELL/HOLD) but insufficient for advanced arbitrage navigation use-cases where
the model must simultaneously learn:

- **Direction** — which path to take (long / short / neutral)
- **Magnitude** — expected return magnitude (normalised distance on price scale)
- **Confidence** — signal reliability score (0–1)

This document specifies the **LabelSchema** type that will replace the scalar label
vector in a future session and enable richer tensor-style label transport across blocks.

---

## Current State (Session 06)

```python
# TrainSplitBlock today:
# y shape = (n_samples,)  dtype=int64  values ∈ {0, 1, 2}
```

```csharp
// TrainSplitBlock.cs today:
// Produces: int[] Labels — 1-D integer class indices
```

---

## Target State (Session 07+)

### Python Side — Multi-Dimensional Labels

```python
from dataclasses import dataclass
from typing import Literal
import numpy as np

@dataclass(frozen=True)
class LabelSchema:
    """Describes the shape and semantics of the label tensor."""

    # Number of columns in the label tensor
    n_dims: int

    # Per-dimension metadata
    names:  tuple[str, ...]           # e.g. ("direction", "magnitude", "confidence")
    dtypes: tuple[np.dtype, ...]      # e.g. (np.int64, np.float32, np.float32)
    ranges: tuple[tuple, ...]         # e.g. ((0, 2), (0.0, 1.0), (0.0, 1.0))


# Example: arbitrage navigation label
ARB_LABEL_SCHEMA = LabelSchema(
    n_dims=3,
    names=("direction", "magnitude", "confidence"),
    dtypes=(np.dtype("int64"), np.dtype("float32"), np.dtype("float32")),
    ranges=((0, 2), (0.0, 1.0), (0.0, 1.0)),
)

# y shape becomes: (n_samples, 3)
# y[:, 0] = direction class index  ∈ {0=long, 1=short, 2=neutral}
# y[:, 1] = magnitude              ∈ [0.0, 1.0]  normalised expected return
# y[:, 2] = confidence             ∈ [0.0, 1.0]  signal reliability
```

### C# Side — LabelSchema Contract

```csharp
namespace MLS.Core.Designer;

/// <summary>
/// Describes the shape and per-dimension semantics of a multi-dimensional
/// label tensor produced by <see cref="TrainSplitBlock"/>.
/// </summary>
public sealed record LabelSchema(
    int NDims,
    string[] DimensionNames,
    LabelDimensionType[] DimensionTypes,
    (double Min, double Max)[] Ranges
);

/// <summary>Classification of each label dimension.</summary>
public enum LabelDimensionType
{
    /// <summary>Integer class index. Compatible with CrossEntropy.</summary>
    ClassIndex,

    /// <summary>Continuous value in [Min, Max]. Compatible with MSELoss or Huber.</summary>
    Continuous,

    /// <summary>Probability value in [0, 1]. Compatible with BinaryCrossEntropy.</summary>
    Probability,
}
```

---

## Tensor Transport Pattern

Tensor-ish label objects have size constraints when transported across block sockets.
The label schema solves this by decoupling the schema descriptor (small, always
transported) from the data tensor (bulk, transported only within training blocks):

```
TrainSplitBlock
    │
    │  Output socket: FeatureVector
    │  Payload: { X: float[n, d], y: float[n, k], schema: LabelSchema }
    │
    ▼
TrainModelBlock
    │  Reads schema.NDims to select loss function:
    │    NDims=1  → CrossEntropy (existing path)
    │    NDims=3  → MultiTaskLoss(CrossEntropy, MSE, BCE) with dimension weights
```

### Wire Schema (Envelope Payload)

```json
{
  "type": "BLOCK_SIGNAL",
  "payload": {
    "signal_type": "FeatureVector",
    "schema": {
      "n_dims": 3,
      "dimension_names": ["direction", "magnitude", "confidence"],
      "dimension_types": ["ClassIndex", "Continuous", "Probability"],
      "ranges": [{ "min": 0, "max": 2 }, { "min": 0.0, "max": 1.0 }, { "min": 0.0, "max": 1.0 }]
    },
    "features_ref": "ipfs://Qm...",
    "labels_ref":   "ipfs://Qm..."
  }
}
```

> **Note**: Large tensors are not serialised inline.  `features_ref` and `labels_ref`
> are IPFS CIDs pointing to the raw binary data.  Only the schema descriptor and
> metadata are transported in the envelope.

---

## Parsing / Distribution Functions

The `LabelSchema` enables downstream blocks to route each dimension to the
correct parsing or distribution function:

| Dimension | Function | Description |
|-----------|----------|-------------|
| `direction` (ClassIndex) | `torch.nn.CrossEntropyLoss` | Class distribution |
| `magnitude` (Continuous) | `torch.nn.HuberLoss` | Regression with outlier robustness |
| `confidence` (Probability) | `torch.nn.BCELoss` | Binary probability calibration |

### Python Training Pipeline Extension

```python
def _build_multitask_loss(schema: LabelSchema) -> torch.nn.Module:
    """Build a composite loss function aligned to the LabelSchema dimensions."""
    losses = []
    for dim_type in schema.dimension_types:
        if dim_type == "ClassIndex":
            losses.append(torch.nn.CrossEntropyLoss())
        elif dim_type == "Continuous":
            losses.append(torch.nn.HuberLoss())
        elif dim_type == "Probability":
            losses.append(torch.nn.BCEWithLogitsLoss())
    return MultiTaskLoss(losses)  # custom wrapper with per-dimension weight
```

---

## Use in Arbitrage Pattern Matching

For `model-a` (arbitrage), the multi-dimensional label encodes:

```
direction=1 (short), magnitude=0.73, confidence=0.91
→ "short the Camelot/DFYN spread with 73% of position size; signal confidence 91%"
```

This is far more actionable than a scalar `1` (short signal) alone.

The `HyperparamSearchBlock` can be extended with a `LabelSchema` parameter that
selects the appropriate loss function profile during hyperparameter search, allowing
the search to optimise across all label dimensions simultaneously.

---

## Acceptance Criteria (Future Session)

- [ ] `LabelSchema` record in `MLS.Core.Designer`
- [ ] `TrainSplitBlock` emits `schema` in output `FeatureVector` payload
- [ ] `TrainModelBlock` reads `schema.NDims` and selects loss function accordingly
- [ ] `training_pipeline.py` `LabelSchema` dataclass round-trips through JSON config
- [ ] `_build_multitask_loss` routes each dimension to the correct PyTorch criterion
- [ ] Unit test: `LabelSchemaTests` — 3 tests: scalar (1-D), direction-only (1-D ClassIndex), full 3-D arbitrage schema
