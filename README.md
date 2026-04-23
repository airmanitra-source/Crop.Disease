<div align="center">

# 🌽 Crop Disease Classifier

<p>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white"/>
  <img src="https://img.shields.io/badge/ML.NET-4.0.2-blueviolet?style=for-the-badge"/>
  <img src="https://img.shields.io/badge/MobileNetV2-TF%20backend-orange?style=for-the-badge"/>
  <img src="https://img.shields.io/badge/Accuracy-94.7%25-brightgreen?style=for-the-badge"/>
  <img src="https://img.shields.io/badge/CPU--only-No GPU-blue?style=for-the-badge"/>
</p>

*Diagnoses maize leaf diseases from a photo — or from a text description when no camera is available.*

</div>

---
## How to run it

 * Unfortunately, I did not have time to build a docker image file so you have to download and install all dependencies of the solution and run it inside Visual Studio 2022
 * Put `corn-maize-lefDiseaseDataset.zip` at the root of the project
 * Create a folder dataset_extracted>data to host unzipped images
 * Run the console app `Crop.Disease.Classifier`
 * Once training is finished (not long actually)
 * You can activate API solution and make your requests (Postman, curl)

   
## Overview

This solution contains two projects:

| Project | Role |
|---|---|
| `Crop.Disease.Classifier` | Training pipeline — trains a MobileNetV2 model on the maize disease dataset and exports `model.zip` + `labels.txt` |
| `Crop.Disease.API` | REST API — loads the trained model and exposes two endpoints: image-based prediction and text-based symptom diagnosis |

---

## Disease classes

| Label | Disease |
|---|---|
| `Blight` | Northern Leaf Blight *(Helminthosporium turcicum)* |
| `Common_Rust` | Common Rust *(Puccinia sorghi)* |
| `Gray_Leaf_Spot` | Gray Leaf Spot *(Cercospora zeae-maydis)* |
| `Healthy` | No disease detected |

---

## Training — `Crop.Disease.Classifier`

### Requirements

- Dataset zip: `corn-maize-lefDiseaseDataset.zip` — place it at the repo root or pass it as an argument
- .NET 10 SDK

### Run

```sh
cd Crop.Disease.Classifier
dotnet run -- [optional: path/to/dataset.zip] [optional: ./output]
```

### What it does
0. Dataset comes from Kaggle: https://www.kaggle.com/datasets/smaranjitghose/corn-or-maize-leaf-disease-dataset
1. Extracts and stratified-samples **1 500 images** from the dataset (balanced per class)
2. Splits into **80 % train / 10 % validation / 10 % test**
3. Fine-tunes **MobileNetV2** (TensorFlow backend via ML.NET) at 224 × 224 input
4. Evaluates on the clean test split — target macro-accuracy >= 80 %
5. Evaluates again on a **noisy augmented set** (brightness, contrast, rotation, blur)
6. Saves artefacts to `./output/`

```
output/
├── model.zip      ← ML.NET model  (~22 MB)
├── labels.txt     ← ordered class names
└── checkpoints/   ← TensorFlow checkpoint (ignored by git)
```

### Training results (sample run)

| Split | Micro-Accuracy |
|---|---|
| Clean test (10 %) | **94.7 %** |
| Noisy augmented eval | ~89 % |

---

## Model size reduction — optional Python script

ML.NET cannot export ImageClassification pipelines to ONNX directly (TF internal nodes).
A Python script is provided to convert the TF checkpoint to ONNX and quantise it to INT8:

```sh
cd scripts
pip install -r requirements.txt
python convert_to_onnx.py
```

| Format | Size | Notes |
|---|---|---|
| `model.zip` (ML.NET) | ~22 MB | Default — used by the API automatically |
| `model.onnx` (FP32) | ~9 MB | Requires Python conversion |
| `model_int8.onnx` (INT8) | ~3 MB | Requires Python conversion — **API preferred format** |

> The API detects `model_int8.onnx` automatically at startup. If present, it uses ONNX Runtime
> for inference (faster, smaller). Otherwise it falls back to `model.zip` via ML.NET.

---

## API — `Crop.Disease.API`

### Prerequisites

Run the Classifier first (or copy existing artefacts). The API copies `model.zip` and `labels.txt`
automatically on build via a post-build target in the `.csproj`.

### Start

```sh
cd Crop.Disease.API
dotnet run
# → https://localhost:7243
```

---

## Endpoint 1 — Image prediction `POST /predict`

For farmers or agents who can take and upload a photo.

**Request:** `multipart/form-data`, field `image` (JPEG or PNG, max 5 MB)

```sh
curl -X POST https://localhost:7243/predict -F "image=@/path/to/leaf.jpg" -k
```

**Response:**

```json
{
  "label": "Common_Rust",
  "confidence": 0.94,
  "top3": [
    { "label": "Common_Rust",    "score": 0.94 },
    { "label": "Blight",         "score": 0.04 },
    { "label": "Gray_Leaf_Spot", "score": 0.02 }
  ],
  "latencyMs": 120,
  "rationale": "Common Rust (confidence: 94%). Recommended action: Apply Mancozeb 80 WP (2 kg/ha).",
  "ussdSmsTemplate": "[RW]Indwara y'umutuku. Koresha Mancozeb 2kg/ha./[EN]Common Rust. Mancozeb 2kg/ha.",
  "lowConfidenceEscalation": false
}
```

> When `lowConfidenceEscalation` is `true` (confidence < 60 %), the SMS template prompts the
> farmer to retake the photo.

---

## Endpoint 2 — Symptom text diagnosis `POST /diagnose/symptoms`

**Fallback for farmers without camera access** — feature phones, USSD *123#, or SMS.

The operator (USSD gateway / SMS aggregator) forwards the farmer's free-text description to this
endpoint. The API matches keywords in **English, French, and Kinyarwanda** and returns a diagnosis
or schedules a technician visit.

```sh
curl -X POST https://localhost:7243/diagnose/symptoms -k \
     -H "Content-Type: application/json" \
     -d '{"description": "red powder on leaves", "phoneNumber": "+250788123456"}'
```

**Response — disease recognised:**

```json
{
  "label": "Common_Rust",
  "confidenceLevel": "High",
  "recommendation": "Common Rust. Apply Mancozeb 80 WP (2 kg/ha). Use resistant seed varieties.",
  "ussdSmsTemplate": "[RW]Indwara y'umutuku. Koresha Mancozeb 2kg/ha./[EN]Common Rust. Mancozeb 2kg/ha.",
  "technicianVisitScheduled": false
}
```

**Response — symptoms not recognised:**

```json
{
  "label": null,
  "confidenceLevel": "Unknown",
  "recommendation": "Symptoms not recognised. An agricultural technician will visit you shortly.",
  "ussdSmsTemplate": "[RW]Umuhanga azaza kwawe./[EN]Technician dispatched. Registered phone: +250788123456",
  "technicianVisitScheduled": true
}
```

### Keyword coverage

The matcher understands descriptions in English, French, and Kinyarwanda:

| Disease | Example keywords |
|---|---|
| Blight | `blight`, `burnt leaves`, `long grey spots`, `brulure`, `taches allongees`, `ikonjesha ibabi` |
| Common Rust | `rust`, `red powder`, `orange pustule`, `rouille`, `poudre rouge`, `umutuku` |
| Gray Leaf Spot | `gray leaf`, `rectangular spot`, `cercospora`, `tache angulaire`, `ibara ry'ibicucu` |
| Healthy | `healthy`, `green`, `no disease`, `sain`, `muzima` |

### Getting the symptom description guide

```sh
curl https://localhost:7243/diagnose/symptoms/guide -k
```

---

## End-to-end workflow

```
         Farmer in the field
               │
       ┌───────┴────────────┐
 Has camera              Feature phone only
       │                     │
       ▼                     ▼
POST /predict       POST /diagnose/symptoms
(upload image)      (describe symptoms in text)
       │                     │
       │              Keywords matched?
       │              Yes → diagnosis + SMS
       │              No  → technician visit scheduled
       ▼
label + confidence + top3 + SMS template
```

---

## Project structure

```
CropDisease/
├── Crop.Disease.Classifier/
│   ├── Models/       ImageData · ImagePrediction
│   ├── Services/     ModelTrainer · OnnxExporter · DatasetPreparer · ImageAugmentor
│   └── Program.cs    Entry point — train → evaluate → export
│
├── Crop.Disease.API/
│   ├── Controllers/  PredictController · DiagnoseController
│   ├── Models/       PredictResponse · SymptomRequest · SymptomDiagnosisResponse
│   ├── Services/     InferenceService · RationaleService · SymptomMatcherService
│   └── Program.cs    DI setup — auto-detects ONNX INT8 vs ML.NET zip
│
└── scripts/
    ├── convert_to_onnx.py   TF checkpoint → ONNX FP32 → INT8 (~3 MB)
    └── requirements.txt
```

---

## Git — ignored files

| Ignored | Reason |
|---|---|
| `dataset_extracted/` | Re-extracted from zip on demand |
| `checkpoints/` | TF checkpoint — regenerated on each training run (~24 MB) |
| `eval_noisy/` | Augmented images — regenerated |
| `*.bin` / bottleneck files | ML.NET cache |
| `model.onnx` | Empty placeholder (ML.NET ONNX export not supported for TF pipelines) |

| Tracked | Why |
|---|---|
| `output/model.zip` | Trained model — required by the API |
| `output/labels.txt` | Class index — required by the API |
| `output/model_int8.onnx` | Optimised model (generated by Python script if run) |
