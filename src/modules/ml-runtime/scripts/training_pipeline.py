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
    epochs:            int = 100
    batch_size:        int = 512
    learning_rate:     float = 1e-3
    dropout_rate:      float = 0.2
    weight_decay:      float = 1e-5
    warmup_steps:      int = 500
    patience:          int = 20
    hidden_dims:       list[int] = field(default_factory=lambda: [128, 64, 32])
    n_classes:         int = 3
    data_from:         str = ""
    data_to:           str = ""

    @classmethod
    def from_payload(cls, payload: dict[str, Any]) -> "TrainingConfig":
        hp      = payload.get("hyperparams", {})
        dr      = payload.get("data_range", {})
        hidden  = hp.get("hidden_dims", [128, 64, 32])
        if isinstance(hidden, str):
            hidden = json.loads(hidden)

        return cls(
            job_id=str(payload["job_id"]),
            model_type=payload["model_type"],
            schema_version=int(payload.get("feature_schema_version", 1)),
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

    Labels are drawn from [0, n_classes) so they are always compatible with
    the model's loss function (binary labels for model-a, 3-class for others).
    """
    rng = np.random.default_rng(42)
    n   = 2048
    X   = rng.standard_normal((n, cfg.input_dim)).astype(np.float32)
    y   = rng.integers(0, cfg.n_classes, n).astype(np.int64)
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


def _emit_info(job_id: str, message: str) -> None:
    msg = {"type": "TRAINING_JOB_INFO", "job_id": job_id, "message": message}
    print(json.dumps(msg), flush=True)


# ─────────────────────────────────────────────────────────────────────────────
# Entry point
# ─────────────────────────────────────────────────────────────────────────────


def _build_model(cfg: TrainingConfig) -> nn.Module:
    if cfg.model_type == "model-a":
        return ModelA(cfg)
    if cfg.model_type == "model-d":
        return ModelD(cfg)
    return ModelT(cfg)  # default: model-t


def run(cfg: TrainingConfig) -> None:
    """Full training run for a single job."""
    t_start = time.monotonic()

    _emit_info(cfg.job_id, f"Loading dataset for {cfg.model_type!r}")
    X, y = load_dataset(cfg)

    X_tr, y_tr, X_val, y_val, X_tst, y_tst = train_val_test_split(X, y)
    _emit_info(
        cfg.job_id,
        f"Dataset: total={len(X)} train={len(X_tr)} val={len(X_val)} test={len(X_tst)}",
    )

    model = _build_model(cfg)
    model = train_loop(model, cfg, X_tr, y_tr, X_val, y_val)

    # ── Versioning ───────────────────────────────────────────────────────────
    version   = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
    model_id  = f"{cfg.model_type}-v{version}"

    onnx_path   = export_onnx(model, cfg, version)
    joblib_path = export_joblib(model, cfg, version)
    ipfs_cid    = upload_to_ipfs(onnx_path)

    metrics      = compute_final_metrics(model, cfg, X_tst, y_tst)
    duration_ms  = int((time.monotonic() - t_start) * 1000)

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
    cfg     = TrainingConfig.from_payload(payload)

    try:
        run(cfg)
    except Exception as exc:
        _emit_error(cfg.job_id, str(exc))
        sys.exit(1)


if __name__ == "__main__":
    main()
