<div align="center">

# Process Log — Crop Disease Classifier

*Development timeline · LLM usage · Tool declarations*

<img src="https://img.shields.io/badge/LLM-GitHub%20Copilot-0078d7?style=for-the-badge&logo=github"/>
<img src="https://img.shields.io/badge/Model-Claude%20Sonnet%204.6-orange?style=for-the-badge"/>
<img src="https://img.shields.io/badge/Accuracy-94.7%25-brightgreen?style=for-the-badge"/>
<img src="https://img.shields.io/badge/CPU--only-No GPU-blue?style=for-the-badge"/>

</div>

---

## Timeline at a Glance

```
Phase 1 ────────────── Phase 2 ────────────── Phase 3 ─────────────── Phase 4
   │                      │                      │                       │
Dataset + Training    Model Export           REST API                Fallback +
Pipeline              & Artefacts            /predict                /diagnose/symptoms
```

---

## Phase 1 — Training Pipeline

### What I did

- Chose **MobileNetV2** as backbone (compact, CPU-friendly, proven on leaf disease datasets)
- Implemented stratified sampling: **1 500 images / 4 classes** from the full dataset
- 80 / 10 / 10 split — train / validation / test
- Integrated ML.NET `ImageClassificationTrainer` with TensorFlow backend
- Added noisy augmentation eval (brightness, contrast, rotation, blur) for robustness check
- Result: **94.7 % micro-accuracy** on clean test split

### Decisions

- Used `SciSharp.TensorFlow.Redist 1.15.0` (not 2.x) — ML.NET Vision requires TF 1.x API
- Checkpoint folder deleted before each run to avoid Windows memory-mapped file lock (`IOException`)

---

## Phase 2 — Model Export

### What I did

- Saved model as `model.zip` (ML.NET native, ~22 MB)
- Attempted ONNX export via `Microsoft.ML.OnnxConverter` — **failed silently** (0-byte file)
- Root cause: ML.NET `ImageClassification` pipelines embed TF internal nodes incompatible with OnnxConverter
- Solution: provided optional Python script (`scripts/convert_to_onnx.py`) using `tf2onnx` + `onnxruntime` quantisation

| Format | Size | How |
|---|---|---|
| `model.zip` | ~22 MB | ML.NET native — default |
| `model.onnx` | ~9 MB | Python tf2onnx (optional) |
| `model_int8.onnx` | ~3 MB | Python INT8 quantisation (optional) |

---

## Phase 3 — REST API (`POST /predict`)

### What I did

- Built ASP.NET Core minimal API
- `InferenceService` supports dual mode: ONNX Runtime (if `model_int8.onnx` present) or ML.NET PredictionEngine (fallback)
- Auto-detects model at startup via `AppContext.BaseDirectory` + `Directory.GetCurrentDirectory()` fallback
- `RationaleService` generates bilingual EN + Kinyarwanda SMS templates (< 160 chars)
- Low-confidence escalation: if softmax score < 60 %, SMS prompts farmer to retake the photo

---

## Phase 4 — Fallback: Text-based Symptom Diagnosis (`POST /diagnose/symptoms`)

### What I did

- Identified that farmers in rural Rwanda often use **feature phones without camera upload capability**
- Built `SymptomMatcherService` with keyword profiles covering **English, French, and Kinyarwanda**
- Scoring: count keyword matches per disease profile
  - Score >= 3 → High confidence → return diagnosis + treatment
  - Score = 2 → Medium confidence → return diagnosis
  - Score = 1 → Low confidence → probable diagnosis + warning
  - Score = 0 or ambiguous tie → escalate to technician visit
- `DiagnoseController` exposes `POST /diagnose/symptoms` and `GET /diagnose/symptoms/guide`

---

## LLM Usage Declaration

| Tool | Usage | Sample prompts |
|---|---|---|
| GitHub Copilot (Claude Sonnet 4.6) | Architecture decisions, bug fixes, code generation | See below |

### Sample prompts

1. *"Why does `TF_StringEncodedSize` throw EntryPointNotFoundException and how do I fix it with SciSharp.TensorFlow.Redist?"*
2. *"The ONNX export produces a 0-byte file. What is the root cause for ML.NET ImageClassification pipelines and what is the alternative?"*
3. *"Build a keyword-based symptom matcher for maize diseases that works in English, French and Kinyarwanda, with technician escalation when confidence is too low."*

---

## Issues encountered

| Issue | Root cause | Fix |
|---|---|---|
| `TF_StringEncodedSize` not found | `SciSharp.TensorFlow.Redist 2.16.0` incompatible with ML.NET Vision | Downgraded to `1.15.0` |
| `GetSlotNames` InvalidOperationException | Called on a `KeyType` column, not a slot-annotated column | Replaced with `GetKeyValues` on the trained schema |
| `model.onnx` is 0 bytes | ML.NET cannot export TF-backed pipelines to ONNX | Kept `model.zip`, added Python conversion script |
| `IOException` on re-training | Windows keeps `.meta.pb` memory-mapped between runs | `Directory.Delete(recursive)` before each training run |
| `FileNotFoundException: model.zip` | `./output/` resolved from project dir in VS, not `bin/` | `ResolvePath()` checks both `AppContext.BaseDirectory` and `GetCurrentDirectory()` |
