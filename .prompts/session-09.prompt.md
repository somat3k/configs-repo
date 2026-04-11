---
mode: agent
description: "BCG Session 09 — ML Runtime Refactor into Hybrid Tensor Service"
status: "⏳ Pending — ML Runtime exists but needs tensor governance alignment"
depends-on: ["session-03", "session-05", "session-08"]
produces: ["docs/bcg/session-09-*.md", "src/modules/ml-runtime/"]
---

# Session 09 — ML Runtime Refactor into Hybrid Tensor Service

> **Status**: ⏳ Pending — `src/modules/ml-runtime/` exists but lacks tensor governance, A/B evaluation, and formal model promotion workflow.

## Session Goal

Refine ML Runtime into a tensor-governed hybrid service that connects training, validation, export, and inference under one governed release policy.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-09-extended-document.md` (source: `.prompts-update/BCG_Session_09_Extended_Document.md`)
- [ ] `ml-runtime-production-spec.md` — hybrid service role: training handoff + inference governance
- [ ] `model-promotion-workflow.md` — Draft → Validated → Staged → Active → Deprecated lifecycle
- [ ] `inference-signature-contract.md` — input/output tensor shapes, dtype policy, signature versioning
- [ ] `ab-shadow-evaluation-design.md` — traffic split, shadow mode, metric comparison, promotion trigger

### ML Runtime Refactor (`src/modules/ml-runtime/MLS.MLRuntime/`)
- [ ] Add `ModelLifecycleState.cs` enum — Draft, Validated, Staged, Active, Deprecated
- [ ] Extend `ModelRegistry` to store `ModelLifecycleState` per model entry
- [ ] Add `ModelPromotionService.cs` — validates artifact (ONNX lint + signature check), transitions state
- [ ] Add `InferenceSignatureValidator.cs` — validates input shape [1,7] and output shape [1,3] for model-t; configurable per model
- [ ] Add `ABEvaluationService.cs` — routes percentage of traffic to challenger model, collects metrics
- [ ] Add `ShadowEvaluationService.cs` — runs challenger in parallel, compares outputs without affecting live traffic
- [ ] Expose `GET /api/models` — list all models with lifecycle state
- [ ] Expose `POST /api/models/{modelId}/promote` — triggers promotion workflow
- [ ] Expose `POST /api/models/{modelId}/rollback` — reverts to previous Active model
- [ ] Emit `MODEL_PROMOTED`, `MODEL_ROLLED_BACK`, `MODEL_VALIDATION_FAILED` envelope events
- [ ] Add `MessageTypes.MLRuntime.cs` for new event constants
- [ ] Add `ModelPromotedPayload.cs`, `ModelRolledBackPayload.cs` to `src/core/MLS.Core/Contracts/`

### Python Alignment (`src/modules/ml-runtime/scripts/`)
- [ ] `training_pipeline.py` already emits `MTF_TRAINING_JOB_COMPLETE` — add `MODEL_EXPORT_MANIFEST` with artifact path and signature info
- [ ] `InferenceWorker` picks up `MODEL_EXPORT_MANIFEST` and triggers `ModelPromotionService`

### Tests (`src/modules/ml-runtime/MLS.MLRuntime.Tests/`)
- [ ] `ModelPromotionServiceTests.cs` — valid artifact → Active; invalid → ValidationFailed
- [ ] `InferenceSignatureValidatorTests.cs` — shape mismatch rejects model
- [ ] `ABEvaluationServiceTests.cs` — traffic split routes correctly to challenger
- [ ] Existing 29 tests must still pass

## Skills to Apply

```
.skills/machine-learning.md          — ONNX, model registry, inference signature
.skills/python.md                    — training_pipeline.py output manifest
.skills/models/model-t.md            — model-t [1,7]→[1,3] signature
.skills/models/model-a.md            — model-a signature
.skills/models/model-d.md            — model-d signature
.skills/dotnet-devs.md               — ConcurrentDictionary, IAsyncDisposable, primary constructors
.skills/storage-data-management.md   — Redis inference cache, PostgreSQL model state
.skills/beast-development.md         — hot-reload swap-first + delayed dispose, zero-alloc inference
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — all model lifecycle events via EnvelopePayload
- `SessionOptions` must be disposed with `using` after `InferenceSession` creation
- Hot-reload: swap-first, then 500 ms delayed dispose of old `InferenceSession`
- ModelId cache key: `{modelKey}:{version}` composite string

## Acceptance Gates

- [ ] `ModelRegistry` correctly tracks lifecycle state for model-t, model-a, model-d
- [ ] `POST /api/models/{modelId}/promote` rejects invalid ONNX signature
- [ ] `ABEvaluationService` routes traffic split within ±2% of declared ratio
- [ ] All 29+ existing tests still pass: `dotnet test src/modules/ml-runtime/MLS.MLRuntime.Tests/`
- [ ] 4 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/modules/ml-runtime/MLS.MLRuntime/Models/ModelRegistry.cs` | Extend with lifecycle state |
| `src/modules/ml-runtime/MLS.MLRuntime/Inference/InferenceEngine.cs` | Signature validation hook |
| `src/modules/ml-runtime/MLS.MLRuntime/Services/InferenceWorker.cs` | Subscribe to MODEL_EXPORT_MANIFEST |
| `src/modules/ml-runtime/scripts/training_pipeline.py` | Add MODEL_EXPORT_MANIFEST output |
| `src/core/MLS.Core/Contracts/` | Add model lifecycle payload records |
| `.prompts-update/BCG_Session_09_Extended_Document.md` | Full session spec |
