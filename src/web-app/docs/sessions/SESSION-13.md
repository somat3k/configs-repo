# web-app — Session 13: ML Runtime Panel + Observatory + Envelope Viewer

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 13. ML Runtime Panel + Observatory + Envelope Viewer

**Phase**: 3 — MDI Canvas Rewrite

**Objective**: Implement ML Runtime dashboard, Module Observatory, and diagnostic Envelope Viewer panels.

---

### Files Created

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/Components/MLRuntime/ModelRegistry.razor` | All models, versions, metrics |
| CREATE | `src/web-app/WebApp/Components/MLRuntime/InferenceMetrics.razor` | Latency histogram, throughput |
| CREATE | `src/web-app/WebApp/Components/MLRuntime/TrainProgress.razor` | Live training: loss curve, confusion matrix |
| CREATE | `src/web-app/WebApp/Components/Observatory/NetworkTopology.razor` | Cytoscape.js module graph |
| CREATE | `src/web-app/WebApp/Components/Observatory/ModuleCard.razor` | Health card per module |
| CREATE | `src/web-app/WebApp/Components/Observatory/EnvelopeStream.razor` | Live filtered envelope viewer |

---

### TrainProgress Live Chart Pattern

```
TRAINING_JOB_PROGRESS envelope received
  → update loss_series[] and accuracy_series[]
  → JS.InvokeVoidAsync("updateApexSeries", "loss-chart", epoch, train_loss, val_loss)
  → JS.InvokeVoidAsync("updateApexSeries", "accuracy-chart", epoch, accuracy)

On TRAINING_JOB_COMPLETE:
  → Render confusion matrix using ApexCharts heatmap
  → Show SHAP feature importance bar chart
```

Envelope topics consumed: `TRAINING_JOB_PROGRESS`, `TRAINING_JOB_COMPLETE`.

Constants used: `MessageTypes.Designer.TrainingJobProgress`, `MessageTypes.Designer.TrainingJobComplete`.

---

### Panel Details

#### ModelRegistry

- Displays all registered ONNX models from ml-runtime module.
- Columns: Model name, type (model-t / model-a / model-d), version, accuracy, F1 score, exported at, status (deployed / staged / archived).
- Model lineage chain: training run → ONNX export → deployed inference endpoint.
- Subscribes to: `INFERENCE_RESULT` (for live accuracy tracking), `TRAINING_JOB_COMPLETE`.

#### InferenceMetrics

- Latency histogram rendered as SVG bar chart (buckets: <1ms, 1-5ms, 5-10ms, 10-50ms, >50ms).
- Throughput sparkline (inferences/sec over 60-second window).
- Per-model type metrics (model-t, model-a, model-d) toggled via tab strip.
- Subscribes to: `INFERENCE_RESULT`.

#### TrainProgress

- Live loss and accuracy curves via ApexCharts line series — one point per epoch, no page re-load.
- Confusion matrix heatmap rendered on `TRAINING_JOB_COMPLETE` via `JS.InvokeVoidAsync("renderConfusionMatrix", ...)`.
- SHAP feature importance bar chart rendered on completion.
- Training meta: model type, total epochs, current epoch, best val_loss, elapsed time.
- Subscribes to: `TRAINING_JOB_PROGRESS`, `TRAINING_JOB_COMPLETE`.

#### NetworkTopology

- Cytoscape.js graph with one node per registered module.
- Node colour encodes health: green = Healthy, amber = Degraded, red = Offline.
- Edges represent active inter-module connections tracked from heartbeat data.
- Node click expands to show uptime, CPU, memory (calls `ModuleCard` as popup).
- Subscribes to: `MODULE_HEARTBEAT`, `MODULE_REGISTER`, `MODULE_DEREGISTER`.

#### ModuleCard

- Standalone health card component; used both inline in Observatory list and as topology node popup.
- Displays: module name, status badge, uptime, CPU%, memory MB, last heartbeat (relative timestamp).
- Status badge colours: Healthy = `#22c55e`, Degraded = `#f59e0b`, Offline = `#f43f5e`.
- Props: `ModuleStatusUpdate` record injected from parent.

#### EnvelopeStream

- Live scrollable feed of envelopes received from Block Controller.
- Regex filter input narrows by message type (e.g. `TRADE_.*` matches all trade topics).
- Type-filter chips (toggleable) for quick filtering by category.
- Each row shows: timestamp, type badge (colour-coded by category), module_id, payload summary (first 120 chars).
- Max buffer: 500 envelopes (circular). Clear button resets display.
- Subscribes to: all topics (`[]` → subscribe-all mode).

---

### Skills Applied

- `.skills/premium-uiux-blazor.md`
- `.skills/machine-learning.md`

---

### Acceptance Criteria

- [x] `TrainProgress` renders live loss curve updating every epoch
- [x] `NetworkTopology` shows all registered modules as graph nodes with edge labels
- [x] `EnvelopeStream` filters by message type with regex search
- [x] `ModelRegistry` shows model lineage (parent training run → exported ONNX → deployed)

**Session Status: ✅ COMPLETE**

---
