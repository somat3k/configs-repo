#!/usr/bin/env python3
"""
hyperparam_search.py — Optuna-based hyperparameter search for MLS models.

Spawned by the Shell VM when it receives a TRAINING_JOB_START envelope with
``hyperparams.search_mode == "optuna"``.  The script runs a complete Optuna
study (TPE sampler + Hyperband pruner) and streams per-trial progress back to
the Designer via the Shell VM stdout relay.

Usage:
    python hyperparam_search.py --config <path_to_json_config>

Config JSON schema (search payload):
    {
        "job_id":                "<uuid>",
        "model_type":            "model-t" | "model-a" | "model-d",
        "feature_schema_version": 1,
        "hyperparams": {
            "search_mode":       "optuna",
            "n_trials":          50,
            "direction":         "maximize",
            "sampler":           "tpe",
            "pruner":            "hyperband",
            "epochs_per_trial":  5,
            "batch_size":        512,
            "search_space": {
                "lr":          [1e-4, 1e-2],
                "dropout":     [0.1, 0.5],
                "hidden_dims": [[64, 32], [128, 64, 32]]
            }
        },
        "data_range": { "from": "<iso8601>", "to": "<iso8601>" }
    }

Stdout protocol (one JSON object per line):
    Trial epoch:   {"type":"TRAINING_JOB_PROGRESS","job_id":"...","trial_index":1,"n_trials":50,
                    "epoch":3,"total_epochs":5,"train_loss":0.5,"val_loss":0.4,"accuracy":0.71,
                    "best_value":0.71,"is_pruned":false,"elapsed_ms":1200,"eta_ms":3000}
    Trial done:    {"type":"TRAINING_JOB_PROGRESS","job_id":"...","trial_index":1,"n_trials":50,
                    "trial_state":"complete","trial_value":0.71,"best_value":0.71,"is_pruned":false,
                    "elapsed_ms":2000,"eta_ms":0}
    Study done:    {"type":"TRAINING_JOB_COMPLETE","job_id":"...","model_type":"model-t",
                    "best_params":{...},"best_value":0.73,"n_trials":50,
                    "n_complete":45,"n_pruned":5,"duration_ms":12000,
                    "metrics":{"best_value":0.73}}
    Error:         {"type":"TRAINING_JOB_ERROR","job_id":"...","error":"..."}
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
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.optim import AdamW
from torch.optim.lr_scheduler import LambdaLR
from torch.utils.data import DataLoader, TensorDataset

# ── Import shared model/data utilities from training_pipeline ─────────────────
_SCRIPTS_DIR = Path(__file__).parent
sys.path.insert(0, str(_SCRIPTS_DIR))

from training_pipeline import (  # noqa: E402
    TrainingConfig,
    ModelT,
    ModelA,
    ModelD,
    _generate_synthetic_features,
    _load_features_from_pg,
    _should_fallback_to_synthetic,
    train_val_test_split,
    _cosine_warmup_schedule,
    _emit_info,
    _emit_error,
    ARTIFACTS_DIR,
)

try:
    import optuna  # type: ignore
    from optuna.samplers import TPESampler  # type: ignore
    from optuna.pruners import HyperbandPruner  # type: ignore
    _OPTUNA_AVAILABLE = True
except ImportError:
    _OPTUNA_AVAILABLE = False


# ─────────────────────────────────────────────────────────────────────────────
# Search config
# ─────────────────────────────────────────────────────────────────────────────


@dataclass
class SearchConfig:
    """Resolved hyperparameter search configuration."""

    job_id:           str
    model_type:       str
    schema_version:   int
    n_trials:         int             = 50
    direction:        str             = "maximize"
    sampler:          str             = "tpe"
    pruner:           str             = "hyperband"
    epochs_per_trial: int             = 5
    batch_size:       int             = 512
    data_from:        str             = ""
    data_to:          str             = ""
    # Search space bounds
    lr_min:           float           = 1e-4
    lr_max:           float           = 1e-2
    dropout_min:      float           = 0.1
    dropout_max:      float           = 0.5
    hidden_dims_choices: list[list[int]] = field(
        default_factory=lambda: [[64, 32], [128, 64, 32]]
    )

    @classmethod
    def from_payload(cls, payload: dict[str, Any]) -> "SearchConfig":
        hp  = payload.get("hyperparams", {})
        dr  = payload.get("data_range", {})
        ss  = hp.get("search_space", {})

        lr_bounds      = ss.get("lr",          [1e-4, 1e-2])
        dropout_bounds = ss.get("dropout",     [0.1, 0.5])
        hd_choices     = ss.get("hidden_dims", [[64, 32], [128, 64, 32]])

        # Normalise hidden_dims_choices: accept strings (JSON-encoded)
        parsed_hd: list[list[int]] = []
        for hd in hd_choices:
            if isinstance(hd, str):
                hd = json.loads(hd)
            parsed_hd.append([int(x) for x in hd])

        return cls(
            job_id=str(payload["job_id"]),
            model_type=payload["model_type"],
            schema_version=int(payload.get("feature_schema_version", 1)),
            n_trials=int(hp.get("n_trials", 50)),
            direction=str(hp.get("direction", "maximize")).lower(),
            sampler=str(hp.get("sampler", "tpe")).lower(),
            pruner=str(hp.get("pruner", "hyperband")).lower(),
            epochs_per_trial=int(hp.get("epochs_per_trial", 5)),
            batch_size=int(hp.get("batch_size", 512)),
            data_from=dr.get("from", ""),
            data_to=dr.get("to", ""),
            lr_min=float(lr_bounds[0]),
            lr_max=float(lr_bounds[1]),
            dropout_min=float(dropout_bounds[0]),
            dropout_max=float(dropout_bounds[1]),
            hidden_dims_choices=parsed_hd,
        )

    def to_training_config(self, lr: float, dropout: float, hidden_dims: list[int]) -> TrainingConfig:
        """Build a TrainingConfig for a single Optuna trial."""
        n_classes = 2 if self.model_type == "model-a" else 3
        return TrainingConfig(
            job_id=self.job_id,
            model_type=self.model_type,
            schema_version=self.schema_version,
            algorithm_type="neural_network",
            epochs=self.epochs_per_trial,
            batch_size=self.batch_size,
            learning_rate=lr,
            dropout_rate=dropout,
            weight_decay=1e-5,
            warmup_steps=max(1, self.epochs_per_trial // 2),
            patience=max(3, self.epochs_per_trial),
            hidden_dims=hidden_dims,
            n_classes=n_classes,
            data_from=self.data_from,
            data_to=self.data_to,
        )


# ─────────────────────────────────────────────────────────────────────────────
# Stdout helpers
# ─────────────────────────────────────────────────────────────────────────────


def _emit_trial_epoch(
    job_id: str,
    trial_index: int,
    n_trials: int,
    epoch: int,
    total_epochs: int,
    train_loss: float,
    val_loss: float,
    accuracy: float,
    best_value: float,
    is_pruned: bool,
    elapsed_ms: int,
    eta_ms: int,
) -> None:
    """Emit per-epoch progress within a trial."""
    msg = {
        "type":         "TRAINING_JOB_PROGRESS",
        "job_id":       job_id,
        "trial_index":  trial_index,
        "n_trials":     n_trials,
        "epoch":        epoch,
        "total_epochs": total_epochs,
        "train_loss":   round(train_loss, 6),
        "val_loss":     round(val_loss, 6),
        "accuracy":     round(accuracy, 6),
        "best_value":   round(best_value, 6),
        "is_pruned":    is_pruned,
        "is_hyperparam_search": True,
        "elapsed_ms":   elapsed_ms,
        "eta_ms":       eta_ms,
    }
    print(json.dumps(msg), flush=True)


def _emit_trial_summary(
    job_id: str,
    trial_index: int,
    n_trials: int,
    trial_value: float,
    best_value: float,
    is_pruned: bool,
    trial_params: dict[str, Any],
    elapsed_ms: int,
) -> None:
    """Emit end-of-trial summary (not an epoch, marks trial completion/pruning)."""
    msg = {
        "type":              "TRAINING_JOB_PROGRESS",
        "job_id":            job_id,
        "trial_index":       trial_index,
        "n_trials":          n_trials,
        "trial_state":       "pruned" if is_pruned else "complete",
        "trial_value":       round(trial_value, 6),
        "trial_params":      trial_params,
        "best_value":        round(best_value, 6),
        "is_pruned":         is_pruned,
        "is_hyperparam_search": True,
        "elapsed_ms":        elapsed_ms,
        "eta_ms":            0,
    }
    print(json.dumps(msg), flush=True)


def _emit_study_complete(
    job_id: str,
    model_type: str,
    best_params: dict[str, Any],
    best_value: float,
    n_trials: int,
    n_complete: int,
    n_pruned: int,
    duration_ms: int,
) -> None:
    msg = {
        "type":        "TRAINING_JOB_COMPLETE",
        "job_id":      job_id,
        "model_type":  model_type,
        "model_id":    f"{model_type}-optuna-best",
        "onnx_path":   "",
        "joblib_path": "",
        "ipfs_cid":    "",
        "best_params": best_params,
        "best_value":  round(best_value, 6),
        "n_trials":    n_trials,
        "n_complete":  n_complete,
        "n_pruned":    n_pruned,
        "duration_ms": duration_ms,
        "metrics": {
            "best_value":  round(best_value, 6),
            "best_params": best_params,
            "n_complete":  int(n_complete),
            "n_pruned":    int(n_pruned),
        },
        "is_hyperparam_search": True,
    }
    print(json.dumps(msg), flush=True)


# ─────────────────────────────────────────────────────────────────────────────
# Data loading (thin wrapper for search context)
# ─────────────────────────────────────────────────────────────────────────────


def _load_dataset_for_search(cfg: SearchConfig) -> tuple[np.ndarray, np.ndarray]:
    """Load dataset once before the study starts; fall back to synthetic on failure."""
    # Build a minimal TrainingConfig to reuse the existing load helpers
    train_cfg = cfg.to_training_config(lr=1e-3, dropout=0.2, hidden_dims=[64, 32])
    try:
        return _load_features_from_pg(train_cfg)
    except ValueError:
        raise
    except Exception as exc:
        if _should_fallback_to_synthetic(exc):
            _emit_info(cfg.job_id, f"PostgreSQL unavailable ({exc!s}); using synthetic data")
            return _generate_synthetic_features(train_cfg)
        raise


# ─────────────────────────────────────────────────────────────────────────────
# Trial training loop with Optuna pruning
# ─────────────────────────────────────────────────────────────────────────────


def _train_trial(
    trial: "optuna.Trial",
    cfg: SearchConfig,
    train_cfg: TrainingConfig,
    X_tr: np.ndarray,
    y_tr: np.ndarray,
    X_val: np.ndarray,
    y_val: np.ndarray,
    trial_index: int,
    n_trials: int,
    best_value_ref: list[float],
    study_start: float,
    total_study_time_estimate: float,
) -> float:
    """
    Run one Optuna trial, streaming epoch-level progress.

    Reports each epoch's validation metric to Optuna for intermediate pruning.
    Returns the final objective value (e.g., validation accuracy = Sharpe proxy).
    Raises ``optuna.TrialPruned`` when the pruner determines the trial should stop.
    """
    device = torch.device("cpu")
    torch.set_num_threads(1)

    # Build and move model to device
    model = _build_search_model(train_cfg).to(device)

    X_tr_t  = torch.from_numpy(X_tr).to(device)
    y_tr_t  = torch.from_numpy(y_tr).to(device)
    X_val_t = torch.from_numpy(X_val).to(device)
    y_val_t = torch.from_numpy(y_val).to(device)

    dataset   = TensorDataset(X_tr_t, y_tr_t)
    loader    = DataLoader(dataset, batch_size=train_cfg.batch_size, shuffle=True)
    optimizer = AdamW(model.parameters(), lr=train_cfg.learning_rate, weight_decay=train_cfg.weight_decay)

    total_steps = train_cfg.epochs * len(loader)
    scheduler   = LambdaLR(
        optimizer,
        lr_lambda=_cosine_warmup_schedule(train_cfg.warmup_steps, total_steps),
    )

    trial_start = time.monotonic()
    best_val    = float("inf")
    final_val   = float("inf")
    final_acc   = 0.0

    for epoch in range(1, train_cfg.epochs + 1):
        model.train()
        epoch_loss = 0.0

        for X_batch, y_batch in loader:
            optimizer.zero_grad(set_to_none=True)
            loss = _compute_loss(model, X_batch, y_batch, train_cfg)
            loss.backward()
            nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
            scheduler.step()
            epoch_loss += loss.item()

        # Validation
        model.eval()
        with torch.no_grad():
            val_loss, accuracy = _compute_val_metrics(model, X_val_t, y_val_t, train_cfg)

        final_val = val_loss
        final_acc = accuracy
        if val_loss < best_val:
            best_val = val_loss

        trial_elapsed_ms   = int((time.monotonic() - trial_start) * 1000)
        study_elapsed_ms   = int((time.monotonic() - study_start) * 1000)
        completed_fraction = (trial_index * train_cfg.epochs + epoch) / max(
            1, n_trials * train_cfg.epochs
        )
        eta_ms = (
            int(study_elapsed_ms / completed_fraction * (1.0 - completed_fraction))
            if completed_fraction > 0
            else 0
        )

        # Objective value for this epoch: use accuracy as Sharpe proxy for all model types.
        # TODO: differentiate by model type when Sharpe ratio data is available from Python pipeline.
        intermediate_value = accuracy

        # Update running best using the configured study direction.
        if math.isnan(best_value_ref[0]) or (
            intermediate_value < best_value_ref[0]
            if cfg.direction == "minimize"
            else intermediate_value > best_value_ref[0]
        ):
            best_value_ref[0] = intermediate_value

        _emit_trial_epoch(
            job_id=cfg.job_id,
            trial_index=trial_index,
            n_trials=n_trials,
            epoch=epoch,
            total_epochs=train_cfg.epochs,
            train_loss=float(epoch_loss / max(1, len(loader))),
            val_loss=float(val_loss),
            accuracy=float(accuracy),
            best_value=float(best_value_ref[0]),
            is_pruned=False,
            elapsed_ms=study_elapsed_ms,
            eta_ms=eta_ms,
        )

        # Report intermediate value to Optuna pruner
        trial.report(intermediate_value, step=epoch)

        if trial.should_prune():
            raise optuna.TrialPruned()

    return float(final_acc)


def _build_search_model(cfg: TrainingConfig) -> nn.Module:
    if cfg.model_type == "model-a":
        return ModelA(cfg)
    if cfg.model_type == "model-d":
        return ModelD(cfg)
    return ModelT(cfg)


def _compute_loss(
    model: nn.Module,
    X_batch: torch.Tensor,
    y_batch: torch.Tensor,
    cfg: TrainingConfig,
) -> torch.Tensor:
    if isinstance(model, ModelT):
        logits, conf = model(X_batch)
        cls_loss     = F.cross_entropy(logits, y_batch)
        pred         = logits.argmax(-1)
        correct      = (pred == y_batch).float().unsqueeze(-1)
        conf_loss    = F.binary_cross_entropy(conf, correct)
        return cls_loss + 0.1 * conf_loss
    if isinstance(model, ModelA):
        out = model(X_batch).squeeze(-1)
        return F.binary_cross_entropy(out, y_batch.float())
    if isinstance(model, ModelD):
        seq    = X_batch[:, : ModelD._SEQ_LEN * ModelD._SEQ_DIM].reshape(-1, ModelD._SEQ_LEN, ModelD._SEQ_DIM)
        static = X_batch[:, ModelD._SEQ_LEN * ModelD._SEQ_DIM :]
        logits = model(seq, static)
        return F.cross_entropy(logits, y_batch)
    logits = model(X_batch)
    return F.cross_entropy(logits, y_batch)


def _compute_val_metrics(
    model: nn.Module,
    X_val_t: torch.Tensor,
    y_val_t: torch.Tensor,
    cfg: TrainingConfig,
) -> tuple[float, float]:
    if isinstance(model, ModelT):
        val_logits, _ = model(X_val_t)
        val_loss      = F.cross_entropy(val_logits, y_val_t).item()
        accuracy      = (val_logits.argmax(-1) == y_val_t).float().mean().item()
    elif isinstance(model, ModelD):
        seq    = X_val_t[:, : ModelD._SEQ_LEN * ModelD._SEQ_DIM].reshape(-1, ModelD._SEQ_LEN, ModelD._SEQ_DIM)
        static = X_val_t[:, ModelD._SEQ_LEN * ModelD._SEQ_DIM :]
        val_out  = model(seq, static)
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
    return float(val_loss), float(accuracy)


# ─────────────────────────────────────────────────────────────────────────────
# Optuna study runner
# ─────────────────────────────────────────────────────────────────────────────


def run_study(cfg: SearchConfig) -> None:
    """Create and run an Optuna study, streaming results to stdout."""
    if not _OPTUNA_AVAILABLE:
        _emit_error(cfg.job_id, "optuna is not installed — cannot run hyperparameter search")
        sys.exit(1)

    # Silence Optuna's own logging (we handle progress ourselves)
    optuna.logging.set_verbosity(optuna.logging.WARNING)

    _emit_info(cfg.job_id,
               f"Starting Optuna study: model={cfg.model_type} n_trials={cfg.n_trials} "
               f"direction={cfg.direction} sampler={cfg.sampler} pruner={cfg.pruner}")

    # Load data once up-front for the full study
    _emit_info(cfg.job_id, "Loading dataset for hyperparameter search …")
    X, y = _load_dataset_for_search(cfg)
    if cfg.model_type == "model-a":
        y = np.clip(y, 0, 1).astype(np.int64)

    X_tr, y_tr, X_val, y_val, _X_tst, _y_tst = train_val_test_split(X, y)
    _emit_info(cfg.job_id,
               f"Dataset: total={len(X)} train={len(X_tr)} val={len(X_val)} test={len(_X_tst)}")

    # Build sampler
    if cfg.sampler == "tpe":
        sampler = TPESampler(seed=42)
    else:
        sampler = TPESampler(seed=42)  # fallback to TPE for any unknown value

    # Build pruner
    if cfg.pruner == "hyperband":
        pruner = HyperbandPruner(
            min_resource=1,
            max_resource=cfg.epochs_per_trial,
            reduction_factor=3,
        )
    else:
        pruner = HyperbandPruner(
            min_resource=1,
            max_resource=cfg.epochs_per_trial,
            reduction_factor=3,
        )

    study = optuna.create_study(
        direction=cfg.direction,
        sampler=sampler,
        pruner=pruner,
    )

    study_start    = time.monotonic()
    best_value_ref = [float("nan")]   # mutable reference updated inside objective
    trial_index    = [0]              # mutable counter (Python closure)

    def objective(trial: "optuna.Trial") -> float:
        idx = trial_index[0]
        trial_index[0] += 1

        # Suggest hyperparameters from search space
        lr = trial.suggest_float(
            "lr",
            cfg.lr_min,
            cfg.lr_max,
            log=True,
        )
        dropout = trial.suggest_float(
            "dropout",
            cfg.dropout_min,
            cfg.dropout_max,
        )
        hd_idx = trial.suggest_categorical(
            "hidden_dims_idx",
            list(range(len(cfg.hidden_dims_choices))),
        )
        hidden_dims = cfg.hidden_dims_choices[hd_idx]

        train_cfg = cfg.to_training_config(lr=lr, dropout=dropout, hidden_dims=hidden_dims)

        try:
            value = _train_trial(
                trial=trial,
                cfg=cfg,
                train_cfg=train_cfg,
                X_tr=X_tr, y_tr=y_tr,
                X_val=X_val, y_val=y_val,
                trial_index=idx,
                n_trials=cfg.n_trials,
                best_value_ref=best_value_ref,
                study_start=study_start,
                total_study_time_estimate=0.0,
            )
        except optuna.TrialPruned:
            elapsed_ms = int((time.monotonic() - study_start) * 1000)
            pruned_val = float(trial.intermediate_values.get(
                max(trial.intermediate_values.keys()) if trial.intermediate_values else 0, 0.0
            ))
            _emit_trial_summary(
                job_id=cfg.job_id,
                trial_index=idx,
                n_trials=cfg.n_trials,
                trial_value=pruned_val,
                best_value=float(best_value_ref[0]) if best_value_ref[0] == best_value_ref[0] else 0.0,
                is_pruned=True,
                trial_params={"lr": lr, "dropout": dropout, "hidden_dims": hidden_dims},
                elapsed_ms=elapsed_ms,
            )
            raise

        elapsed_ms = int((time.monotonic() - study_start) * 1000)

        # Update running best using the configured study direction.
        if math.isnan(best_value_ref[0]) or (
            value > best_value_ref[0] if cfg.direction == "maximize" else value < best_value_ref[0]
        ):
            best_value_ref[0] = value

        _emit_trial_summary(
            job_id=cfg.job_id,
            trial_index=idx,
            n_trials=cfg.n_trials,
            trial_value=value,
            best_value=float(best_value_ref[0]),
            is_pruned=False,
            trial_params={"lr": lr, "dropout": dropout, "hidden_dims": hidden_dims},
            elapsed_ms=elapsed_ms,
        )
        return value

    study.optimize(objective, n_trials=cfg.n_trials)

    # ── Summarise study results ───────────────────────────────────────────────
    duration_ms = int((time.monotonic() - study_start) * 1000)

    complete_trials = [t for t in study.trials if t.state == optuna.trial.TrialState.COMPLETE]
    pruned_trials   = [t for t in study.trials if t.state == optuna.trial.TrialState.PRUNED]

    best_trial  = study.best_trial
    best_params = {
        "lr":          best_trial.params.get("lr",          1e-3),
        "dropout":     best_trial.params.get("dropout",     0.2),
        "hidden_dims": cfg.hidden_dims_choices[
            best_trial.params.get("hidden_dims_idx", 0)
        ],
    }

    _emit_study_complete(
        job_id=cfg.job_id,
        model_type=cfg.model_type,
        best_params=best_params,
        best_value=float(study.best_value),
        n_trials=cfg.n_trials,
        n_complete=len(complete_trials),
        n_pruned=len(pruned_trials),
        duration_ms=duration_ms,
    )


# ─────────────────────────────────────────────────────────────────────────────
# Entry point
# ─────────────────────────────────────────────────────────────────────────────


def main() -> None:
    parser = argparse.ArgumentParser(description="MLS Optuna hyperparameter search")
    parser.add_argument("--config", required=True, help="Path to JSON config file")
    args = parser.parse_args()

    config_path = Path(args.config)
    if not config_path.exists():
        print(json.dumps({
            "type":  "TRAINING_JOB_ERROR",
            "job_id": "",
            "error": f"Config file not found: {config_path}",
        }), flush=True)
        sys.exit(1)

    with config_path.open() as f:
        payload = json.load(f)

    job_id = str(payload.get("job_id", ""))

    try:
        cfg = SearchConfig.from_payload(payload)
    except Exception as exc:
        print(json.dumps({
            "type":  "TRAINING_JOB_ERROR",
            "job_id": job_id,
            "error": f"Config parse error: {exc!s}",
        }), flush=True)
        sys.exit(1)

    try:
        run_study(cfg)
    except KeyboardInterrupt:
        _emit_error(job_id, "Search interrupted by user")
        sys.exit(130)
    except Exception as exc:
        _emit_error(job_id, str(exc))
        sys.exit(1)


if __name__ == "__main__":
    main()
