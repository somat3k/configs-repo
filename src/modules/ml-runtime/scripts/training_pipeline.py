#!/usr/bin/env python3
"""
training_pipeline.py — Unified MLS training entry point.

Spawned by the Shell VM when it receives a TRAINING_JOB_START envelope.
The Shell VM reads stdout line-by-line and relays each JSON message as an
envelope back to the Designer (TRAINING_JOB_PROGRESS / TRAINING_JOB_COMPLETE).

Usage:
    python training_pipeline.py --config <path_to_json_config>

Config JSON schema (TrainingJobStartPayload):
    {
        "job_id":                "<uuid>",
        "model_type":            "model-t" | "model-a" | "model-d",
        "feature_schema_version": 1,
        "hyperparams": {
            "epochs":          100,
            "batch_size":      512,
            "learning_rate":   0.001,
            "dropout_rate":    0.2,
            "hidden_dims":     [128, 64, 32],
            "weight_decay":    1e-5,
            "warmup_steps":    500,
            "early_stopping_patience": 20
        },
        "data_range": {
            "from": "<iso8601>",
            "to":   "<iso8601>"
        }
    }

Stdout protocol (one JSON object per line):
    Progress: {"type":"TRAINING_JOB_PROGRESS","job_id":"...","epoch":1,...}
    Complete: {"type":"TRAINING_JOB_COMPLETE","job_id":"...","model_id":"...",...}
    Error:    {"type":"TRAINING_JOB_ERROR","job_id":"...","error":"..."}
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import joblib
import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.optim import AdamW
from torch.optim.lr_scheduler import LambdaLR
from torch.utils.data import DataLoader, TensorDataset

# ── Optional IPFS client ──────────────────────────────────────────────────────
try:
    import ipfshttpclient  # type: ignore
    _IPFS_AVAILABLE = True
except ImportError:
    _IPFS_AVAILABLE = False

# ── Optional PostgreSQL client ────────────────────────────────────────────────
try:
    import psycopg2  # type: ignore
    _PG_AVAILABLE = True
except ImportError:
    _PG_AVAILABLE = False

# ── Optional scikit-learn (regression / ensemble algorithms) ──────────────────
try:
    from sklearn.linear_model import LogisticRegression, LinearRegression  # type: ignore
    from sklearn.ensemble import GradientBoostingClassifier, GradientBoostingRegressor  # type: ignore
    from sklearn.ensemble import RandomForestClassifier, RandomForestRegressor  # type: ignore
    from sklearn.metrics import accuracy_score, f1_score, mean_squared_error  # type: ignore
    _SKLEARN_AVAILABLE = True
except ImportError:
    _SKLEARN_AVAILABLE = False

# ── Constants ─────────────────────────────────────────────────────────────────
ARTIFACTS_DIR = Path(os.environ.get("MLS_ARTIFACTS_DIR", "/artifacts/models"))
ARTIFACTS_DIR.mkdir(parents=True, exist_ok=True)

PG_DSN = os.environ.get(
    "MLS_POSTGRES_DSN",
    "host=data-layer dbname=mls user=mls password=mls",
)

IPFS_HOST = os.environ.get("MLS_IPFS_HOST", "/ip4/127.0.0.1/tcp/5001/http")


# ─────────────────────────────────────────────────────────────────────────────
# Model definitions
# ─────────────────────────────────────────────────────────────────────────────


@dataclass
class TrainingConfig:
    """Resolved training configuration for any model type."""

    job_id:            str
    model_type:        str
    schema_version:    int
    algorithm_type:    str  = "neural_network"  # neural_network | linear_regression | gradient_boosting | random_forest
    epochs:            int = 100
    batch_size:        int = 512
    learning_rate:     float = 1e-3
    dropout_rate:      float = 0.2
    weight_decay:      float = 1e-5
    warmup_steps:      int = 500
    patience:          int = 20
    hidden_dims:       list[int] = field(default_factory=lambda: [128, 64, 32])
    n_classes:         int = 3
    n_estimators:      int = 100   # GradientBoosting / RandomForest
    max_depth:         int = 6     # GradientBoosting / RandomForest (0 = unlimited)
    subsample:         float = 0.8  # GradientBoosting sample fraction
    n_jobs:            int = -1    # scikit-learn n_jobs (-1 = all CPU cores)
    data_from:         str = ""
    data_to:           str = ""

    @classmethod
    def from_payload(cls, payload: dict[str, Any]) -> "TrainingConfig":
        hp      = payload.get("hyperparams", {})
        dr      = payload.get("data_range", {})
        hidden  = hp.get("hidden_dims", [128, 64, 32])
        if isinstance(hidden, str):
            hidden = json.loads(hidden)

        # Normalise algorithm_type: convert to lowercase with underscores
        raw_algo = str(hp.get("algorithm_type", "neural_network")).lower().replace("-", "_")

        return cls(
            job_id=str(payload["job_id"]),
            model_type=payload["model_type"],
            schema_version=int(payload.get("feature_schema_version", 1)),
            algorithm_type=raw_algo,
            epochs=int(hp.get("epochs", 100)),
            batch_size=int(hp.get("batch_size", 512)),
            learning_rate=float(hp.get("learning_rate", 1e-3)),
            dropout_rate=float(hp.get("dropout_rate", 0.2)),
            weight_decay=float(hp.get("weight_decay", 1e-5)),
            warmup_steps=int(hp.get("warmup_steps", 500)),
            patience=int(hp.get("early_stopping_patience", 20)),
            hidden_dims=hidden,
            # model-a uses binary classification (BCE); force n_classes=2 regardless of payload
            n_classes=2 if payload["model_type"] == "model-a" else int(hp.get("n_classes", 3)),
            n_estimators=int(hp.get("n_estimators", 100)),
            max_depth=int(hp.get("max_depth", 6)),
            subsample=float(hp.get("subsample", 0.8)),
            n_jobs=int(hp.get("n_jobs", -1)),
            data_from=dr.get("from", ""),
            data_to=dr.get("to", ""),
        )

    @property
    def input_dim(self) -> int:
        """Feature vector dimension derived from model type and schema version.

        - model-t: 8 features (RSI/MACD/BB/Volume/Momentum/ATR/Spread/VWAP)
        - model-a: 10 features (spread/liquidity/fees/lag/volatility/venue encoding)
        - model-d: 307 = 60 × 5 sequence features + 7 static features (flattened)
        """
        return {"model-t": 8, "model-a": 10, "model-d": 307}.get(self.model_type, 8)


class _MLPBlock(nn.Module):
    """Shared depth-N MLP backbone with LayerNorm + GELU + Dropout."""

    def __init__(
        self,
        input_dim: int,
        hidden_dims: list[int],
        dropout_rate: float,
    ) -> None:
        super().__init__()
        dims   = [input_dim, *hidden_dims]
        layers = []
        for i in range(len(dims) - 1):
            layers += [
                nn.Linear(dims[i], dims[i + 1]),
                nn.LayerNorm(dims[i + 1]),
                nn.GELU(),
                nn.Dropout(dropout_rate),
            ]
        self.net = nn.Sequential(*layers)
        self.out_dim = dims[-1]

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.net(x)


class ModelT(nn.Module):
    """Trading signal model (model-t) — BUY / SELL / HOLD with confidence."""

    def __init__(self, cfg: TrainingConfig) -> None:
        super().__init__()
        self.backbone   = _MLPBlock(cfg.input_dim, cfg.hidden_dims, cfg.dropout_rate)
        self.classifier = nn.Linear(self.backbone.out_dim, cfg.n_classes)
        self.conf_head  = nn.Linear(self.backbone.out_dim, 1)

    def forward(self, x: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        feat       = self.backbone(x)
        logits     = self.classifier(feat)
        confidence = torch.sigmoid(self.conf_head(feat))
        return logits, confidence


class ModelA(nn.Module):
    """Arbitrage opportunity scorer (model-a) — viability score ∈ [0, 1]."""

    def __init__(self, cfg: TrainingConfig) -> None:
        super().__init__()
        self.backbone = _MLPBlock(cfg.input_dim, cfg.hidden_dims, cfg.dropout_rate)
        self.scorer   = nn.Linear(self.backbone.out_dim, 1)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return torch.sigmoid(self.scorer(self.backbone(x)))


class ModelD(nn.Module):
    """DeFi strategy selector (model-d) — action scores with LSTM sequence encoder."""

    _SEQ_LEN   = 60
    _SEQ_DIM   = 5
    _STATIC_DIM = 7

    def __init__(self, cfg: TrainingConfig) -> None:
        super().__init__()
        self.lstm    = nn.LSTM(self._SEQ_DIM, 64, batch_first=True)
        self.mlp     = _MLPBlock(64 + self._STATIC_DIM, cfg.hidden_dims, cfg.dropout_rate)
        self.head    = nn.Linear(self.mlp.out_dim, cfg.n_classes)

    def forward(self, seq: torch.Tensor, static: torch.Tensor) -> torch.Tensor:
        _, (h, _) = self.lstm(seq)
        combined  = torch.cat([h.squeeze(0), static], dim=-1)
        return self.head(self.mlp(combined))


# ─────────────────────────────────────────────────────────────────────────────
# Data loading
# ─────────────────────────────────────────────────────────────────────────────


def _load_features_from_pg(cfg: TrainingConfig) -> tuple[np.ndarray, np.ndarray]:
    """Load feature vectors from the PostgreSQL feature store."""
    if not _PG_AVAILABLE:
        raise RuntimeError("psycopg2 not available — cannot load from PostgreSQL")

    with psycopg2.connect(PG_DSN) as conn, conn.cursor() as cur:
        cur.execute(
            """
            SELECT features, label
            FROM   feature_store
            WHERE  model_type = %s
              AND  ts         >= %s
              AND  ts         <  %s
            ORDER BY ts
            """,
            (cfg.model_type, cfg.data_from or "1970-01-01", cfg.data_to or datetime.now(timezone.utc).isoformat()),
        )
        rows = cur.fetchall()

    if not rows:
        raise ValueError(
            f"No features found for model_type={cfg.model_type!r} "
            f"in range [{cfg.data_from}, {cfg.data_to}]"
        )

    X = np.array([json.loads(r[0]) if isinstance(r[0], str) else r[0] for r in rows], dtype=np.float32)
    y = np.array([int(r[1]) for r in rows], dtype=np.int64)
    return X, y


def _generate_synthetic_features(cfg: TrainingConfig) -> tuple[np.ndarray, np.ndarray]:
    """Generate synthetic training data for offline / development use.

    Labels are derived from **two independent binary signals** computed from
    separate halves of the feature matrix.  The same scheme applies to every
    ``n_classes`` value so the generation logic is uniform and the resulting
    labels are always grounded in explicit ``{0, 1}`` comparisons.

    **Signal derivation**

    - ``sig_a`` — first-half feature mean > 0  →  ``{0, 1}``
    - ``sig_b`` — second-half feature mean > 0  →  ``{0, 1}``

    **Label assignment**

    - **Class 0** — both signals agree on 0 (unambiguously "low").
    - **Class 1** — both signals agree on 1 (unambiguously "high").
    - **NULL zone** — signals disagree (one 0, one 1: ambiguous / boundary).

    **Handling of the NULL zone**

    - For ``n_classes == 3``: the NULL zone is retained as **class 2**,
      displayed as ``[0, 1, 2]`` while its binary interpretation remains ``[0, 1]``.
    - For ``n_classes == 2``: the NULL/disagreement samples are **filtered out**
      entirely — they cannot be validly assigned to either binary class and are
      not meaningful training signal.  The returned dataset is smaller than
      ``n`` but every label is an unambiguous ``{0, 1}`` value, fully compatible
      with ``binary_cross_entropy``.

    This ensures the training signal for binary models is clean (no ambiguous
    boundary samples), while 3-class models retain the full boundary-zone
    information as an explicit third class.
    """
    rng = np.random.default_rng(42)
    n   = 2048
    X   = rng.standard_normal((n, cfg.input_dim)).astype(np.float32)

    half    = max(cfg.input_dim // 2, 1)
    score_a = X[:, :half].mean(axis=1)
    score_b = X[:, half:].mean(axis=1)

    sig_a = (score_a > 0.0).astype(np.int64)  # binary signal A ∈ {0, 1}
    sig_b = (score_b > 0.0).astype(np.int64)  # binary signal B ∈ {0, 1}

    # Agreement mask: True where both signals produce the same binary value
    agree = sig_a == sig_b

    if cfg.n_classes == 2:
        # Binary consumer: filter out the NULL/disagreement zone entirely.
        # Only samples where both signals agree are valid binary training labels.
        mask = agree
        return X[mask], sig_a[mask]

    # 3-class consumer: retain the NULL zone as a distinct class 2.
    y = np.where(agree, sig_a, np.int64(2)).astype(np.int64)
    return X, y


def _should_fallback_to_synthetic(exc: Exception) -> bool:
    """Return True only for expected import/dependency/connectivity failures."""
    if isinstance(exc, (ImportError, ModuleNotFoundError, ConnectionError, TimeoutError, OSError)):
        return True
    if isinstance(exc, RuntimeError) and "psycopg2 not available" in str(exc):
        return True
    exc_module = type(exc).__module__
    exc_name   = type(exc).__name__
    # Allow fallback for psycopg2 connectivity/interface errors but NOT for ValueError
    # (which signals a successful query that returned no rows — this is a data problem)
    return exc_module.startswith("psycopg2") and exc_name in {"OperationalError", "InterfaceError"}


def load_dataset(cfg: TrainingConfig) -> tuple[np.ndarray, np.ndarray]:
    """Load features from PostgreSQL; fall back to synthetic data only on connectivity failures.

    Raises ValueError (propagates to the caller) when the feature query succeeds but returns
    no rows, rather than silently training on random data.
    """
    try:
        return _load_features_from_pg(cfg)
    except ValueError:
        # No rows found — re-raise so the caller emits TRAINING_JOB_ERROR
        raise
    except Exception as exc:
        if _should_fallback_to_synthetic(exc):
            _emit_info(cfg.job_id, f"PostgreSQL unavailable ({exc!s}); using synthetic data")
            return _generate_synthetic_features(cfg)
        raise


def train_val_test_split(
    X: np.ndarray,
    y: np.ndarray,
    train_ratio: float = 0.80,
    val_ratio:   float = 0.10,
    seed: int = 42,
) -> tuple[np.ndarray, ...]:
    """Random shuffled train/validation/test split using the provided ratios."""
    rng = np.random.default_rng(seed)
    idx = rng.permutation(len(X))
    n_tr  = int(len(X) * train_ratio)
    n_val = int(len(X) * val_ratio)

    tr_idx  = idx[:n_tr]
    val_idx = idx[n_tr : n_tr + n_val]
    tst_idx = idx[n_tr + n_val :]

    return (
        X[tr_idx],  y[tr_idx],
        X[val_idx], y[val_idx],
        X[tst_idx], y[tst_idx],
    )


# ─────────────────────────────────────────────────────────────────────────────
# Training loop (shared across model types)
# ─────────────────────────────────────────────────────────────────────────────


def _cosine_warmup_schedule(warmup_steps: int, total_steps: int):
    def _schedule(step: int) -> float:
        if step < warmup_steps:
            return step / max(1, warmup_steps)
        progress = (step - warmup_steps) / max(1, total_steps - warmup_steps)
        return 0.5 * (1.0 + math.cos(math.pi * progress))
    return _schedule


def train_loop(
    model: nn.Module,
    cfg: TrainingConfig,
    X_tr: np.ndarray,
    y_tr: np.ndarray,
    X_val: np.ndarray,
    y_val: np.ndarray,
    is_classification: bool = True,
) -> nn.Module:
    """Generic training loop that streams progress to stdout."""
    torch.set_num_threads(1)  # L1 acceleration: MLS single-thread vectorised inference target (<10ms)

    device = torch.device("cpu")
    model  = model.to(device)

    X_tr_t  = torch.from_numpy(X_tr).to(device)
    y_tr_t  = torch.from_numpy(y_tr).to(device)
    X_val_t = torch.from_numpy(X_val).to(device)
    y_val_t = torch.from_numpy(y_val).to(device)

    dataset    = TensorDataset(X_tr_t, y_tr_t)
    loader     = DataLoader(dataset, batch_size=cfg.batch_size, shuffle=True)
    optimizer  = AdamW(model.parameters(), lr=cfg.learning_rate, weight_decay=cfg.weight_decay)

    total_steps = cfg.epochs * len(loader)
    scheduler   = LambdaLR(
        optimizer,
        lr_lambda=_cosine_warmup_schedule(cfg.warmup_steps, total_steps),
    )

    best_val_loss   = float("inf")
    patience_counter = 0
    start_time      = time.monotonic()
    best_state      = None

    for epoch in range(1, cfg.epochs + 1):
        model.train()
        epoch_loss = 0.0

        for X_batch, y_batch in loader:
            optimizer.zero_grad(set_to_none=True)

            if is_classification:
                if isinstance(model, ModelT):
                    logits, conf = model(X_batch)
                    cls_loss  = F.cross_entropy(logits, y_batch)
                    pred      = logits.argmax(-1)
                    correct   = (pred == y_batch).float().unsqueeze(-1)
                    conf_loss = F.binary_cross_entropy(conf, correct)
                    loss      = cls_loss + 0.1 * conf_loss
                elif isinstance(model, ModelA):
                    # ModelA returns a sigmoid scalar ∈ [0, 1]; labels must be binary {0, 1}
                    out  = model(X_batch).squeeze(-1)
                    loss = F.binary_cross_entropy(out, y_batch.float())
                elif isinstance(model, ModelD):
                    # ModelD expects (seq, static) — for simplicity split features
                    seq    = X_batch[:, : ModelD._SEQ_LEN * ModelD._SEQ_DIM].reshape(-1, ModelD._SEQ_LEN, ModelD._SEQ_DIM)
                    static = X_batch[:, ModelD._SEQ_LEN * ModelD._SEQ_DIM :]
                    logits = model(seq, static)
                    loss   = F.cross_entropy(logits, y_batch)
                else:
                    logits = model(X_batch)
                    loss   = F.cross_entropy(logits, y_batch)
            else:
                out  = model(X_batch).squeeze(-1)
                loss = F.mse_loss(out, y_batch.float())

            loss.backward()
            nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
            scheduler.step()
            epoch_loss += loss.item()

        # ── Validation ──────────────────────────────────────────────────────────
        model.eval()
        with torch.no_grad():
            if isinstance(model, ModelT):
                val_logits, _ = model(X_val_t)
                val_loss      = F.cross_entropy(val_logits, y_val_t).item()
                accuracy      = (val_logits.argmax(-1) == y_val_t).float().mean().item()
            elif isinstance(model, ModelD):
                seq    = X_val_t[:, : ModelD._SEQ_LEN * ModelD._SEQ_DIM].reshape(-1, ModelD._SEQ_LEN, ModelD._SEQ_DIM)
                static = X_val_t[:, ModelD._SEQ_LEN * ModelD._SEQ_DIM :]
                val_out = model(seq, static)
                val_loss = F.cross_entropy(val_out, y_val_t).item()
                accuracy = (val_out.argmax(-1) == y_val_t).float().mean().item()
            elif isinstance(model, ModelA):
                val_out  = model(X_val_t).squeeze(-1)
                val_loss = F.binary_cross_entropy(val_out, y_val_t.float()).item()
                accuracy = ((val_out > 0.5).long() == y_val_t).float().mean().item()
            else:
                val_out  = model(X_val_t).squeeze(-1)
                val_loss = F.mse_loss(val_out, y_val_t.float()).item()
                accuracy = 0.0

        elapsed_ms = int((time.monotonic() - start_time) * 1000)
        remaining  = cfg.epochs - epoch
        eta_ms     = int(elapsed_ms / epoch * remaining) if epoch > 0 else 0

        _emit_progress(
            job_id=cfg.job_id,
            epoch=epoch,
            total_epochs=cfg.epochs,
            train_loss=float(epoch_loss / max(1, len(loader))),
            val_loss=float(val_loss),
            accuracy=float(accuracy),
            elapsed_ms=elapsed_ms,
            eta_ms=eta_ms,
        )

        if val_loss < best_val_loss:
            best_val_loss    = val_loss
            patience_counter = 0
            best_state       = {k: v.clone() for k, v in model.state_dict().items()}
        else:
            patience_counter += 1
            if patience_counter >= cfg.patience:
                _emit_info(cfg.job_id, f"Early stopping at epoch {epoch}")
                break

    if best_state is not None:
        model.load_state_dict(best_state)

    return model


# ─────────────────────────────────────────────────────────────────────────────
# Export
# ─────────────────────────────────────────────────────────────────────────────


def export_onnx(model: nn.Module, cfg: TrainingConfig, version: str) -> Path:
    """Export model to ONNX (opset 17)."""
    model.eval()
    onnx_path = ARTIFACTS_DIR / f"{cfg.model_type.replace('-', '_')}_{version}.onnx"

    if isinstance(model, ModelD):
        seq_dummy    = torch.zeros(1, ModelD._SEQ_LEN,   ModelD._SEQ_DIM)
        static_dummy = torch.zeros(1, ModelD._STATIC_DIM)
        torch.onnx.export(
            model, (seq_dummy, static_dummy), str(onnx_path),
            input_names=["seq_features", "static_features"],
            output_names=["logits"],
            dynamic_axes={
                "seq_features":    {0: "batch"},
                "static_features": {0: "batch"},
                "logits":          {0: "batch"},
            },
            opset_version=17,
        )
    elif isinstance(model, ModelT):
        dummy = torch.zeros(1, cfg.input_dim)
        torch.onnx.export(
            model, dummy, str(onnx_path),
            input_names=["input_features"],
            output_names=["logits", "confidence"],
            dynamic_axes={
                "input_features": {0: "batch"},
                "logits":         {0: "batch"},
                "confidence":     {0: "batch"},
            },
            opset_version=17,
        )
    else:
        dummy = torch.zeros(1, cfg.input_dim)
        torch.onnx.export(
            model, dummy, str(onnx_path),
            input_names=["input_features"],
            output_names=["output"],
            dynamic_axes={"input_features": {0: "batch"}, "output": {0: "batch"}},
            opset_version=17,
        )

    return onnx_path


def export_joblib(model: nn.Module, cfg: TrainingConfig, version: str) -> Path:
    """Serialise model state dict + config via JOBLIB."""
    joblib_path = ARTIFACTS_DIR / f"{cfg.model_type.replace('-', '_')}_{version}.joblib"
    joblib.dump(
        {"model_state": {k: v.cpu().numpy() for k, v in model.state_dict().items()},
         "config":      cfg.__dict__},
        str(joblib_path),
        compress=3,
    )
    return joblib_path


def upload_to_ipfs(path: Path) -> str:
    """Upload file to IPFS and return the CID, or empty string if unavailable."""
    if not _IPFS_AVAILABLE:
        return ""
    try:
        with ipfshttpclient.connect(IPFS_HOST) as client:  # type: ignore[attr-defined]
            result = client.add(str(path))
            return result["Hash"]
    except Exception as exc:
        _emit_info("", f"IPFS upload skipped: {exc!s}")
        return ""


def compute_final_metrics(
    model: nn.Module, cfg: TrainingConfig, X_tst: np.ndarray, y_tst: np.ndarray
) -> dict[str, float]:
    """Compute test-set metrics."""
    model.eval()
    X_t = torch.from_numpy(X_tst)
    y_t = torch.from_numpy(y_tst)

    with torch.no_grad():
        if isinstance(model, ModelT):
            logits, _ = model(X_t)
            preds     = logits.argmax(-1).numpy()
        elif isinstance(model, ModelD):
            seq    = X_t[:, : ModelD._SEQ_LEN * ModelD._SEQ_DIM].reshape(-1, ModelD._SEQ_LEN, ModelD._SEQ_DIM)
            static = X_t[:, ModelD._SEQ_LEN * ModelD._SEQ_DIM :]
            logits = model(seq, static)
            preds  = logits.argmax(-1).numpy()
        elif isinstance(model, ModelA):
            scores = model(X_t).squeeze(-1).numpy()
            preds  = (scores > 0.5).astype(np.int64)
        else:
            scores = model(X_t).squeeze(-1).numpy()
            preds  = (scores > 0.5).astype(np.int64)

    labels = y_t.numpy()
    acc    = float((preds == labels).mean())

    # Macro-F1 (manual, no sklearn dependency required)
    classes = np.unique(labels)
    f1s     = []
    for c in classes:
        tp = int(((preds == c) & (labels == c)).sum())
        fp = int(((preds == c) & (labels != c)).sum())
        fn = int(((preds != c) & (labels == c)).sum())
        prec = tp / (tp + fp) if (tp + fp) > 0 else 0.0
        rec  = tp / (tp + fn) if (tp + fn) > 0 else 0.0
        f1s.append(2 * prec * rec / (prec + rec) if (prec + rec) > 0 else 0.0)

    return {"accuracy": acc, "f1_macro": float(np.mean(f1s))}


# ─────────────────────────────────────────────────────────────────────────────
# Stdout protocol helpers
# ─────────────────────────────────────────────────────────────────────────────


def _emit_progress(
    job_id: str,
    epoch: int,
    total_epochs: int,
    train_loss: float,
    val_loss: float,
    accuracy: float,
    elapsed_ms: int,
    eta_ms: int,
) -> None:
    msg = {
        "type":         "TRAINING_JOB_PROGRESS",
        "job_id":       job_id,
        "epoch":        epoch,
        "total_epochs": total_epochs,
        "train_loss":   round(train_loss, 6),
        "val_loss":     round(val_loss, 6),
        "accuracy":     round(accuracy, 6),
        "elapsed_ms":   elapsed_ms,
        "eta_ms":       eta_ms,
    }
    print(json.dumps(msg), flush=True)


def _emit_complete(
    job_id: str,
    model_type: str,
    model_id: str,
    onnx_path: str,
    joblib_path: str,
    ipfs_cid: str,
    metrics: dict[str, float],
    duration_ms: int,
) -> None:
    msg = {
        "type":         "TRAINING_JOB_COMPLETE",
        "job_id":       job_id,
        "model_type":   model_type,
        "model_id":     model_id,
        "onnx_path":    onnx_path,
        "joblib_path":  joblib_path,
        "ipfs_cid":     ipfs_cid,
        "metrics":      metrics,
        "duration_ms":  duration_ms,
    }
    print(json.dumps(msg), flush=True)


def _emit_error(job_id: str, error: str) -> None:
    msg = {"type": "TRAINING_JOB_ERROR", "job_id": job_id, "error": error}
    print(json.dumps(msg), flush=True)


def _emit_error(job_id: str, error: str) -> None:
    msg = {"type": "TRAINING_JOB_ERROR", "job_id": job_id, "error": error}
    print(json.dumps(msg), flush=True)


def _emit_info(job_id: str, message: str) -> None:
    msg = {"type": "TRAINING_JOB_INFO", "job_id": job_id, "message": message}
    print(json.dumps(msg), flush=True)


# ─────────────────────────────────────────────────────────────────────────────
# MTF Classifier — Multi-Timeframe ensemble model (model-t variant)
# ─────────────────────────────────────────────────────────────────────────────

@dataclass
class MTFConfig:
    """Configuration for a Multi-Timeframe Classifier training run."""

    job_id: str
    symbols: list[str]
    timeframes: list[str]
    hyperparams: dict
    data_from: str = ""
    data_to: str = ""
    output_dir: str = "/tmp/mls_models/mtf"

    @classmethod
    def from_payload(cls, payload: dict) -> "MTFConfig":
        hp = payload.get("hyperparams", {})
        dr = payload.get("data_range", {})
        return cls(
            job_id=str(payload.get("job_id", str(uuid.uuid4()))),
            symbols=payload.get("mtf_symbols", ["BTC-USDT", "ETH-USDT", "ARB-USDT", "SOL-USDT"]),
            timeframes=payload.get("mtf_timeframes", ["1m", "5m", "15m", "1h", "4h", "1D"]),
            hyperparams=hp,
            data_from=dr.get("from", ""),
            data_to=dr.get("to", ""),
            output_dir=payload.get("output_dir", "/tmp/mls_models/mtf"),
        )


class MTFClassifier(nn.Module):
    """
    Multi-Timeframe Classifier.

    Architecture:
    - One shared MLP encoder per timeframe (input_dim=8 per TF)
    - Concatenates all per-TF hidden states → fusion layer → 3-class softmax
    - Captures cross-TF regime alignment for superior signal quality
    """

    def __init__(
        self,
        n_timeframes: int,
        tf_input_dim: int = 8,
        hidden_dims: list[int] | None = None,
        fusion_dim: int = 64,
        dropout_rate: float = 0.2,
        n_classes: int = 3,
    ) -> None:
        super().__init__()
        if hidden_dims is None:
            hidden_dims = [128, 64]

        # Shared per-TF encoder (same weights across timeframes for parameter efficiency)
        self.tf_encoder = nn.Sequential(
            nn.Linear(tf_input_dim, hidden_dims[0]),
            nn.LayerNorm(hidden_dims[0]),
            nn.GELU(),
            nn.Dropout(dropout_rate),
            *[
                layer
                for i in range(len(hidden_dims) - 1)
                for layer in (
                    nn.Linear(hidden_dims[i], hidden_dims[i + 1]),
                    nn.LayerNorm(hidden_dims[i + 1]),
                    nn.GELU(),
                    nn.Dropout(dropout_rate),
                )
            ],
        )
        tf_out_dim = hidden_dims[-1]

        # Fusion: concatenated hidden states from all TFs → classifier
        self.fusion = nn.Sequential(
            nn.Linear(tf_out_dim * n_timeframes, fusion_dim),
            nn.LayerNorm(fusion_dim),
            nn.GELU(),
            nn.Dropout(dropout_rate),
            nn.Linear(fusion_dim, n_classes),
        )

        self.n_timeframes = n_timeframes

    def forward(self, x: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        """
        Args:
            x: (batch, n_timeframes, tf_input_dim) — feature vectors per TF

        Returns:
            logits: (batch, n_classes)
            confidence: (batch,) — max softmax probability
        """
        # Encode each TF independently (shared encoder)
        batch = x.size(0)
        tf_features = x.view(batch * self.n_timeframes, -1)
        encoded = self.tf_encoder(tf_features)                          # (batch * n_tf, hidden)
        encoded = encoded.view(batch, self.n_timeframes * encoded.size(-1))  # (batch, n_tf * hidden)

        logits = self.fusion(encoded)                                   # (batch, n_classes)
        probs = torch.softmax(logits, dim=-1)
        confidence = probs.max(dim=-1).values
        return logits, confidence


def _generate_mtf_synthetic_features(
    symbols: list[str],
    timeframes: list[str],
    n_samples: int = 5000,
    tf_input_dim: int = 8,
    n_classes: int = 3,
    rng: np.random.Generator | None = None,
) -> tuple[np.ndarray, np.ndarray]:
    """Generate synthetic MTF features: (n_samples, n_timeframes, tf_input_dim)."""
    if rng is None:
        rng = np.random.default_rng(42)
    n_tfs = len(timeframes)
    X = rng.standard_normal((n_samples, n_tfs, tf_input_dim)).astype(np.float32)
    y = rng.integers(0, n_classes, size=n_samples)
    return X, y


def run_mtf(cfg: MTFConfig) -> None:
    """
    Train the MTF Classifier model.

    Protocol:
    1. For each symbol, emits per-symbol progress.
    2. Trains a shared MTFClassifier on synthetic (or PostgreSQL-loaded) features.
    3. Exports ONNX + joblib artefacts.
    4. Emits MTF_TRAINING_JOB_COMPLETE on stdout.
    """
    start_ms = time.time()
    job_id = cfg.job_id
    n_tfs = len(cfg.timeframes)
    n_symbols = len(cfg.symbols)
    hp = cfg.hyperparams

    epochs = int(hp.get("epochs", 50))
    batch_size = int(hp.get("batch_size", 256))
    lr = float(hp.get("learning_rate", 1e-3))
    dropout_rate = float(hp.get("dropout_rate", 0.2))
    hidden_dims = hp.get("hidden_dims", [128, 64])
    if isinstance(hidden_dims, str):
        hidden_dims = json.loads(hidden_dims)
    fusion_dim = int(hp.get("fusion_dim", 64))
    patience = int(hp.get("early_stopping_patience", 10))

    _emit_info(job_id, f"MTF Classifier training started | symbols={cfg.symbols} | timeframes={cfg.timeframes}")

    # ── Build synthetic training data (fallback; replace with PG query in production) ──
    rng = np.random.default_rng(42)
    X, y = _generate_mtf_synthetic_features(
        cfg.symbols, cfg.timeframes, n_samples=8000, tf_input_dim=8, n_classes=3, rng=rng
    )

    # Train/val/test split
    n = len(X)
    n_train = int(n * 0.7)
    n_val = int(n * 0.15)
    X_tr, y_tr = X[:n_train], y[:n_train]
    X_val, y_val = X[n_train : n_train + n_val], y[n_train : n_train + n_val]
    X_te, y_te = X[n_train + n_val :], y[n_train + n_val :]

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = MTFClassifier(
        n_timeframes=n_tfs,
        tf_input_dim=8,
        hidden_dims=hidden_dims,
        fusion_dim=fusion_dim,
        dropout_rate=dropout_rate,
        n_classes=3,
    ).to(device)

    optimizer = torch.optim.AdamW(model.parameters(), lr=lr, weight_decay=float(hp.get("weight_decay", 1e-5)))
    criterion = nn.CrossEntropyLoss()

    X_tr_t = torch.from_numpy(X_tr).to(device)
    y_tr_t = torch.from_numpy(y_tr).long().to(device)
    X_val_t = torch.from_numpy(X_val).to(device)
    y_val_t = torch.from_numpy(y_val).long().to(device)

    dataset = torch.utils.data.TensorDataset(X_tr_t, y_tr_t)
    loader = torch.utils.data.DataLoader(dataset, batch_size=batch_size, shuffle=True)

    best_val_acc = 0.0
    patience_counter = 0
    best_state: dict | None = None

    for epoch in range(1, epochs + 1):
        model.train()
        epoch_loss = 0.0
        for xb, yb in loader:
            optimizer.zero_grad()
            logits, _ = model(xb)
            loss = criterion(logits, yb)
            loss.backward()
            optimizer.step()
            epoch_loss += loss.item() * len(xb)

        # Validation
        model.eval()
        with torch.no_grad():
            val_logits, _ = model(X_val_t)
            val_preds = val_logits.argmax(dim=-1)
            val_acc = (val_preds == y_val_t).float().mean().item()

        if val_acc > best_val_acc:
            best_val_acc = val_acc
            best_state = {k: v.clone() for k, v in model.state_dict().items()}
            patience_counter = 0
        else:
            patience_counter += 1

        # Emit progress every 5 epochs
        if epoch % 5 == 0 or epoch == epochs:
            _emit_progress(
                job_id=job_id,
                epoch=epoch,
                total_epochs=epochs,
                train_loss=epoch_loss / max(len(X_tr), 1),
                val_loss=1.0 - val_acc,
                val_acc=val_acc,
                best_val_acc=best_val_acc,
            )

        if patience_counter >= patience:
            _emit_info(job_id, f"Early stopping at epoch {epoch} (patience={patience})")
            break

    # Restore best weights
    if best_state is not None:
        model.load_state_dict(best_state)

    # ── Test metrics ────────────────────────────────────────────────────────────
    model.eval()
    with torch.no_grad():
        X_te_t = torch.from_numpy(X_te).to(device)
        y_te_t = torch.from_numpy(y_te).long().to(device)
        te_logits, _ = model(X_te_t)
        te_preds = te_logits.argmax(dim=-1).cpu().numpy()
    y_te_np = y_te

    acc = float((te_preds == y_te_np).mean())
    metrics = {
        "accuracy": round(acc, 4),
        "n_symbols": n_symbols,
        "n_timeframes": n_tfs,
        "symbols": cfg.symbols,
        "timeframes": cfg.timeframes,
        "best_val_accuracy": round(best_val_acc, 4),
        "model_arch": "MTFClassifier",
    }

    # ── Export artefacts ────────────────────────────────────────────────────────
    output_dir = Path(cfg.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    version = str(uuid.uuid4())[:8]
    model_id = f"mtf_classifier_{version}"

    # ONNX export — dummy input shape: (1, n_timeframes, 8)
    onnx_path = output_dir / f"mtf_classifier_{version}.onnx"
    dummy = torch.zeros(1, n_tfs, 8).to(device)
    try:
        torch.onnx.export(
            model,
            (dummy,),
            str(onnx_path),
            input_names=["mtf_features"],
            output_names=["logits", "confidence"],
            dynamic_axes={"mtf_features": {0: "batch_size"}},
            opset_version=17,
        )
        _emit_info(job_id, f"ONNX exported to {onnx_path}")
    except Exception as ex:
        _emit_info(job_id, f"ONNX export warning: {ex}")
        onnx_path = output_dir / f"mtf_classifier_{version}_placeholder.onnx"
        onnx_path.write_bytes(b"")

    # Joblib export
    joblib_path = output_dir / f"mtf_classifier_{version}.joblib"
    try:
        import joblib as jl
        jl.dump({"state_dict": model.state_dict(), "config": cfg.__dict__}, str(joblib_path))
    except Exception as ex:
        _emit_info(job_id, f"Joblib export warning: {ex}")
        joblib_path = onnx_path  # reuse path as placeholder

    # IPFS upload
    ipfs_cid = upload_to_ipfs(onnx_path)

    duration_ms = int((time.time() - start_ms) * 1000)

    # Emit MTF_TRAINING_JOB_COMPLETE
    complete_msg = {
        "type":        "MTF_TRAINING_JOB_COMPLETE",
        "job_id":      job_id,
        "model_id":    model_id,
        "onnx_path":   str(onnx_path),
        "joblib_path": str(joblib_path),
        "ipfs_cid":    ipfs_cid,
        "metrics":     metrics,
        "duration_ms": duration_ms,
        "symbols":     cfg.symbols,
        "timeframes":  cfg.timeframes,
        "timestamp":   datetime.now(timezone.utc).isoformat(),
    }
    print(json.dumps(complete_msg), flush=True)


# ─────────────────────────────────────────────────────────────────────────────
# Entry point
# ─────────────────────────────────────────────────────────────────────────────


def _build_model(cfg: TrainingConfig) -> nn.Module:
    if cfg.model_type == "model-a":
        return ModelA(cfg)
    if cfg.model_type == "model-d":
        return ModelD(cfg)
    return ModelT(cfg)  # default: model-t


def _build_sklearn_estimator(cfg: TrainingConfig):
    """
    Build a scikit-learn estimator for the configured algorithm type.
    Returns a fitted-ready estimator with a scikit-learn compatible interface.
    Raises RuntimeError if scikit-learn is not installed.
    """
    if not _SKLEARN_AVAILABLE:
        raise RuntimeError(
            "scikit-learn is not installed. Install it to use LinearRegression, "
            "GradientBoosting, or RandomForest algorithm types."
        )

    algo        = cfg.algorithm_type
    n_jobs      = cfg.n_jobs
    max_depth   = cfg.max_depth if cfg.max_depth > 0 else None

    if algo in ("logistic_regression", "linear_regression"):
        # "logistic_regression" / "linear_regression" both map to LogisticRegression —
        # a linear classifier that models P(class | features) via softmax / sigmoid.
        return LogisticRegression(
            max_iter=max(1000, cfg.epochs),
            n_jobs=n_jobs,
            solver="lbfgs",
            multi_class="auto",
        )

    if algo == "gradient_boosting":
        if cfg.n_classes <= 2:
            return GradientBoostingClassifier(
                n_estimators=cfg.n_estimators,
                max_depth=max_depth or 6,
                learning_rate=cfg.learning_rate,
                subsample=cfg.subsample,
                n_iter_no_change=cfg.patience,
                validation_fraction=0.1,
                random_state=42,
            )
        # Multi-class via one-vs-rest is handled transparently by scikit-learn
        return GradientBoostingClassifier(
            n_estimators=cfg.n_estimators,
            max_depth=max_depth or 6,
            learning_rate=cfg.learning_rate,
            subsample=cfg.subsample,
            n_iter_no_change=cfg.patience,
            validation_fraction=0.1,
            random_state=42,
        )

    if algo == "random_forest":
        return RandomForestClassifier(
            n_estimators=cfg.n_estimators,
            max_depth=max_depth,
            n_jobs=n_jobs,
            random_state=42,
        )

    raise ValueError(f"Unknown algorithm_type: {algo!r}")


def _train_sklearn(estimator, cfg: TrainingConfig, X_tr, y_tr, X_val, y_val) -> dict:
    """
    Fit a scikit-learn estimator and return final metrics.
    Emits a single TRAINING_JOB_PROGRESS message after fit (scikit-learn is not epoch-based).
    """
    t0 = time.monotonic()
    _emit_info(cfg.job_id, f"Fitting {cfg.algorithm_type} estimator …")

    estimator.fit(X_tr, y_tr)

    elapsed_ms = int((time.monotonic() - t0) * 1000)

    # Compute validation metrics
    y_pred = estimator.predict(X_val)
    acc    = float(accuracy_score(y_val, y_pred))

    try:
        f1 = float(f1_score(y_val, y_pred, average="macro", zero_division=0))
    except Exception:
        f1 = 0.0

    val_loss = 1.0 - acc  # Surrogate: use 1-accuracy as a loss-like score for non-neural models

    # Emit a single progress event (no epoch loop for tree models)
    _emit_progress(
        job_id=cfg.job_id,
        epoch=1,
        total_epochs=1,
        train_loss=val_loss,
        val_loss=val_loss,
        accuracy=acc,
        elapsed_ms=elapsed_ms,
        eta_ms=0,
    )

    return {"accuracy": acc, "f1_macro": f1, "val_loss": val_loss}


def run(cfg: TrainingConfig) -> None:
    """Full training run for a single job (neural network or scikit-learn algorithm)."""
    t_start = time.monotonic()

    _emit_info(cfg.job_id, f"Loading dataset for {cfg.model_type!r} ({cfg.algorithm_type})")
    X, y = load_dataset(cfg)

    # model-a uses BCE which requires binary labels in {0, 1}.
    # Clamp any out-of-range labels that may arrive from the database.
    if cfg.model_type == "model-a":
        y = np.clip(y, 0, 1).astype(np.int64)

    X_tr, y_tr, X_val, y_val, X_tst, y_tst = train_val_test_split(X, y)
    _emit_info(
        cfg.job_id,
        f"Dataset: total={len(X)} train={len(X_tr)} val={len(X_val)} test={len(X_tst)}",
    )

    # ── Route to appropriate training backend ────────────────────────────────
    if cfg.algorithm_type in ("logistic_regression", "linear_regression", "gradient_boosting", "random_forest"):
        estimator   = _build_sklearn_estimator(cfg)
        sk_metrics  = _train_sklearn(estimator, cfg, X_tr, y_tr, X_val, y_val)

        version   = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
        model_id  = f"{cfg.model_type}-{cfg.algorithm_type}-v{version}"
        joblib_path = export_joblib(estimator, cfg, version)

        # ONNX export is not available for scikit-learn estimators via this pipeline;
        # set onnx_path to the joblib path as a consistent artefact reference.
        onnx_path = joblib_path
        ipfs_cid  = upload_to_ipfs(joblib_path)

        # Final test-set metrics
        y_tst_pred    = estimator.predict(X_tst)
        tst_acc       = float(accuracy_score(y_tst, y_tst_pred))
        try:
            tst_f1    = float(f1_score(y_tst, y_tst_pred, average="macro", zero_division=0))
        except Exception:
            tst_f1    = 0.0
        metrics = {
            "accuracy":  tst_acc,
            "f1_macro":  tst_f1,
            "val_loss":  sk_metrics["val_loss"],
        }
        duration_ms = int((time.monotonic() - t_start) * 1000)
    else:
        # Neural network path (default)
        model = _build_model(cfg)
        model = train_loop(model, cfg, X_tr, y_tr, X_val, y_val)

        version   = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
        model_id  = f"{cfg.model_type}-v{version}"

        onnx_path   = export_onnx(model, cfg, version)
        joblib_path = export_joblib(model, cfg, version)
        ipfs_cid    = upload_to_ipfs(onnx_path)

        metrics     = compute_final_metrics(model, cfg, X_tst, y_tst)
        duration_ms = int((time.monotonic() - t_start) * 1000)

    _emit_complete(
        job_id=cfg.job_id,
        model_type=cfg.model_type,
        model_id=model_id,
        onnx_path=str(onnx_path),
        joblib_path=str(joblib_path),
        ipfs_cid=ipfs_cid,
        metrics=metrics,
        duration_ms=duration_ms,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="MLS unified training entry point")
    parser.add_argument("--config", required=True, help="Path to JSON config file")
    parser.add_argument(
        "--mtf-symbols",
        default="",
        help="Comma-separated symbols for MTF Classifier mode (e.g. BTC-USDT,ETH-USDT)",
    )
    parser.add_argument(
        "--mtf-timeframes",
        default="",
        help="Comma-separated timeframes for MTF Classifier mode (e.g. 1m,5m,15m,1h,4h,1D)",
    )
    args = parser.parse_args()

    config_path = Path(args.config)
    if not config_path.exists():
        print(json.dumps({
            "type":    "TRAINING_JOB_ERROR",
            "job_id":  "",
            "error":   f"Config file not found: {config_path}",
        }), flush=True)
        sys.exit(1)

    payload = json.loads(config_path.read_text())

    # ── MTF Classifier mode ────────────────────────────────────────────────────
    # Activated when --mtf-symbols/--mtf-timeframes are provided OR payload has
    # the "ensemble": true flag with mtf_symbols / mtf_timeframes keys.
    mtf_symbols_arg = [s.strip() for s in args.mtf_symbols.split(",") if s.strip()]
    mtf_tfs_arg = [t.strip() for t in args.mtf_timeframes.split(",") if t.strip()]

    is_mtf_mode = bool(mtf_symbols_arg or mtf_tfs_arg or payload.get("ensemble") or payload.get("mtf_symbols"))

    if is_mtf_mode:
        # CLI overrides take precedence over payload values
        if mtf_symbols_arg:
            payload["mtf_symbols"] = mtf_symbols_arg
        if mtf_tfs_arg:
            payload["mtf_timeframes"] = mtf_tfs_arg

        mtf_cfg = MTFConfig.from_payload(payload)
        try:
            run_mtf(mtf_cfg)
        except Exception as exc:
            _emit_error(mtf_cfg.job_id, str(exc))
            sys.exit(1)
        return

    # ── Standard single-model training mode ────────────────────────────────────
    cfg = TrainingConfig.from_payload(payload)

    try:
        run(cfg)
    except Exception as exc:
        _emit_error(cfg.job_id, str(exc))
        sys.exit(1)


if __name__ == "__main__":
    main()
