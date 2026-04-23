"""
EN: Converts the ML.NET TF checkpoint to ONNX FP32, then quantises to INT8.
FR: Convertit le checkpoint TF ML.NET en ONNX FP32, puis quantise en INT8.

Usage:
    pip install -r requirements.txt
    python convert_to_onnx.py

Output:
    ../Crop.Disease.Classifier/bin/Debug/net10.0/output/model.onnx        (FP32, ~9 MB)
    ../Crop.Disease.Classifier/bin/Debug/net10.0/output/model_int8.onnx   (INT8, ~3 MB)
"""

import os
import sys
import glob
import numpy as np

# ── 1. Paths ─────────────────────────────────────────────────────────────────

SCRIPT_DIR   = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR   = os.path.join(SCRIPT_DIR, "..", "Crop.Disease.Classifier",
                            "bin", "Debug", "net10.0", "output")
CKPT_DIR     = os.path.join(OUTPUT_DIR, "checkpoints")
LABELS_FILE  = os.path.join(OUTPUT_DIR, "labels.txt")
ONNX_FP32    = os.path.join(OUTPUT_DIR, "model.onnx")
ONNX_INT8    = os.path.join(OUTPUT_DIR, "model_int8.onnx")

# ── 2. Locate the .pb frozen graph produced by ML.NET ────────────────────────

pb_files = glob.glob(os.path.join(CKPT_DIR, "*.pb"))
if not pb_files:
    print("[ERROR] No .pb file found in", CKPT_DIR)
    sys.exit(1)

PB_PATH = pb_files[0]
print(f"[convert] Using frozen graph: {PB_PATH} ({os.path.getsize(PB_PATH)/1024:.0f} KB)")

# ── 3. Load labels ────────────────────────────────────────────────────────────

with open(LABELS_FILE) as f:
    labels = [l.strip() for l in f if l.strip()]
num_classes = len(labels)
print(f"[convert] Classes ({num_classes}): {labels}")

# ── 4. Convert frozen .pb → ONNX FP32 via tf2onnx ────────────────────────────

import subprocess
result = subprocess.run([
    sys.executable, "-m", "tf2onnx.convert",
    "--graphdef", PB_PATH,
    "--output",   ONNX_FP32,
    "--inputs",   "input:0",        # ML.NET MobileNetV2 default input node
    "--outputs",  "output:0",       # ML.NET output node (softmax scores)
    "--opset",    "13",
], capture_output=True, text=True)

if result.returncode != 0:
    print("[tf2onnx STDERR]", result.stderr)
    print("[ERROR] tf2onnx conversion failed.")
    print("Tip: check --inputs/--outputs node names with:")
    print("  python -c \"import tensorflow as tf; g=tf.compat.v1.GraphDef(); g.ParseFromString(open(r'{}','rb').read()); [print(n.name) for n in g.node]\"".format(PB_PATH))
    sys.exit(1)

fp32_mb = os.path.getsize(ONNX_FP32) / (1024*1024)
print(f"[convert] ONNX FP32 saved: {ONNX_FP32} ({fp32_mb:.1f} MB)")

# ── 5. Quantise FP32 → INT8 via onnxruntime ──────────────────────────────────

from onnxruntime.quantization import quantize_dynamic, QuantType

quantize_dynamic(
    model_input   = ONNX_FP32,
    model_output  = ONNX_INT8,
    weight_type   = QuantType.QInt8,
)

int8_mb = os.path.getsize(ONNX_INT8) / (1024*1024)
print(f"[convert] ONNX INT8 saved: {ONNX_INT8} ({int8_mb:.1f} MB)")
print(f"\n[convert] Size reduction: {fp32_mb:.1f} MB → {int8_mb:.1f} MB "
      f"({100*(1-int8_mb/fp32_mb):.0f}% smaller)")
print("\n[convert] Done. Copy model_int8.onnx to the API output folder.")
