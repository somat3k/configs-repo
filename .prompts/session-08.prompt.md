---
mode: agent
description: "BCG Session 08 — TensorTrainer Module"
status: "⏳ Pending — new module required"
depends-on: ["session-05", "session-06", "session-07"]
produces: ["docs/bcg/session-08-*.md", "src/modules/tensor-trainer/"]
---

# Session 08 — TensorTrainer Module

> **Status**: ⏳ Pending — `tensor-trainer` module does not exist yet.

## Session Goal

Create the heavy-compute training module for science, mathematics, code, and text-generation tasks using CPU-optimized and GPU-acceleration-aware pipelines, with BCG-governed artifact lifecycle.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-08-extended-document.md` (source: `.prompts-update/BCG_Session_08_Extended_Document.md`)
- [ ] `tensor-trainer-charter.md` — module mission, ports, capabilities, species classification
- [ ] `training-pipeline-spec.md` — training task model for science/math/code/text workloads
- [ ] `artifact-lifecycle-plan.md` — checkpoint, export, IPFS storage, registry registration
- [ ] `model-registry-contract.md` — artifact naming, versioning, promotion gates
- [ ] `compute-resource-policy.md` — CPU thread pinning, GPU detection, memory budget

### New Module (`src/modules/tensor-trainer/`)
- [ ] Scaffold `MLS.TensorTrainer` .NET 9 project
- [ ] Register with Block Controller; 5-second heartbeat
- [ ] Implement `TensorTrainerHub` SignalR hub (`/hubs/tensor-trainer`)
- [ ] Implement `EnvelopeSender`
- [ ] Create `ITrainingTask` interface — `TrainAsync(TrainingConfig, CancellationToken): IAsyncEnumerable<TrainingProgress>`
- [ ] Create `TrainingConfig.cs` — workload class (Science/Math/Code/Text), dataset ref, hyperparameters, export format
- [ ] Create `TrainingProgress.cs` — epoch, loss, accuracy, elapsedMs, checkpointRef
- [ ] Create `TrainingOrchestrator.cs` — spawns Python training script, streams progress events
- [ ] Create `ArtifactExporter.cs` — converts Python output to ONNX, stores to IPFS, registers to `ModelRegistry`
- [ ] Create `TrainerController` — `POST /api/train`, `GET /api/train/{jobId}/status`
- [ ] Emit `TRAINING_JOB_STARTED`, `TRAINING_JOB_PROGRESS`, `TRAINING_JOB_COMPLETE`, `TRAINING_JOB_FAILED`
- [ ] `TrainModelBlock` emits `TRAINING_JOB_START` envelope — never calls Python directly from block graph
- [ ] Add Dockerfile EXPOSE for assigned ports; add to `docker-compose.yml` and `MLS.sln`

### Python Script (`src/modules/tensor-trainer/scripts/`)
- [ ] `tensor_trainer.py` — PyTorch training loop with `torch.compile(mode="max-autotune")` + AMP bf16
- [ ] `DataLoader(num_workers=4, pin_memory=True)` for all data loading paths
- [ ] Accepts CLI args: `--workload`, `--config`, `--output-dir`, `--epochs`
- [ ] Emits JSON progress lines to stdout, `TRAINING_JOB_COMPLETE` on success

### Tests (`src/modules/tensor-trainer/MLS.TensorTrainer.Tests/`)
- [ ] `TrainingOrchestratorTests.cs` — spawns mock Python, streams progress events
- [ ] `ArtifactExporterTests.cs` — ONNX file verified, IPFS CID returned
- [ ] `TrainerControllerTests.cs` — POST /api/train returns jobId; GET status returns progress

## Skills to Apply

```
.skills/machine-learning.md          — PyTorch, ONNX export, model registry
.skills/python.md                    — training_pipeline.py pattern, torch.compile, AMP
.skills/acceleration/acceleration.md — CPU thread control, L1/L2/L3 cache warm, GC Server mode
.skills/dotnet-devs.md               — IAsyncEnumerable<T>, Channel<T>, Process spawn
.skills/storage-data-management.md   — IPFS artifact storage, PostgreSQL model registry
.skills/models/model-t.md            — model-t training conventions (reference)
.skills/models/model-a.md            — model-a training conventions (reference)
.skills/models/model-d.md            — model-d training conventions (reference)
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — all events via typed EnvelopePayload
- `TrainModelBlock` MUST emit envelope event — NEVER call Python directly from block code
- Model artifacts named: `model_t_*`, `model_a_*`, `model_d_*` per model naming conventions
- Python training uses `torch.compile(mode="max-autotune")` + AMP bf16 + `DataLoader(num_workers=4, pin_memory=True)`

## Acceptance Gates

- [ ] `POST /api/train` with a Science workload config creates a job and streams progress
- [ ] Python script emits `TRAINING_JOB_COMPLETE` and `ArtifactExporter` stores to IPFS
- [ ] `ModelRegistry` in ML Runtime can resolve the exported artifact
- [ ] All tests pass: `dotnet test src/modules/tensor-trainer/MLS.TensorTrainer.Tests/`
- [ ] Module in `docker-compose.yml` and `MLS.sln`
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/modules/tensor-trainer/` | New module root |
| `src/modules/ml-runtime/scripts/training_pipeline.py` | Reference Python training script pattern |
| `src/modules/ml-runtime/MLS.MLRuntime/Models/ModelRegistry.cs` | Target for artifact registration |
| `src/core/MLS.Core/Tensor/TensorPersistenceRef.cs` | IPFS CID storage reference |
| `.prompts-update/BCG_Session_08_Extended_Document.md` | Full session spec |
