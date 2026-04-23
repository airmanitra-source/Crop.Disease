using Microsoft.ML;
using Microsoft.ML.Data;

namespace Crop.Disease.Classifier.Services
{
    /// <summary>
    /// EN: Exports the trained ML.NET model to ONNX format (INT8 quantised target < 10 MB).
    ///     ML.NET generates an FP32 ONNX graph; INT8 quantisation is applied afterwards
    ///     via onnxruntime-tools (Python, optional) or accepted as FP32 when the file is already < 10 MB.
    ///
    /// FR: Exporte le modèle ML.NET entraîné au format ONNX (quantisé INT8, cible < 10 MB).
    ///     ML.NET génère un graphe ONNX FP32 ; la quantisation INT8 est appliquée ensuite
    ///     via onnxruntime-tools (Python, optionnel) ou acceptée en FP32 si le fichier est déjà < 10 MB.
    /// </summary>
    public class OnnxExporter
    {
        private readonly MLContext _mlContext;

        public OnnxExporter(MLContext mlContext)
        {
            _mlContext = mlContext;
        }

        /// <summary>
        /// EN: Saves the model in ML.NET zip format for fast reloading.
        /// FR: Sauvegarde le modèle au format zip ML.NET pour un rechargement rapide.
        /// </summary>
        public void SaveMlNet(ITransformer model, IDataView sampleData, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            _mlContext.Model.Save(model, sampleData.Schema, outputPath);
            long sizeKb = new FileInfo(outputPath).Length / 1024;
            Console.WriteLine($"[OnnxExporter] ML.NET model saved: {outputPath} ({sizeKb} KB)");
        }

        /// <summary>
        /// EN: Exports the model to ONNX format.
        ///     Requires a pipeline compatible with ONNX conversion (no raw TF-only nodes).
        ///     For ImageClassification TF pipelines, falls back to the ML.NET zip with guidance.
        ///
        /// FR: Exporte le modèle au format ONNX.
        ///     Nécessite un pipeline compatible avec la conversion ONNX (pas de nœuds TF purs).
        ///     Pour les pipelines ImageClassification TF, bascule vers le zip ML.NET avec instructions.
        /// </summary>
        public void ExportToOnnx(ITransformer model, IDataView sampleData, string onnxOutputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(onnxOutputPath)!);
            try
            {
                using var fileStream = File.Create(onnxOutputPath);
                _mlContext.Model.ConvertToOnnx(model, sampleData, fileStream);
                fileStream.Flush();

                long sizeMb = new FileInfo(onnxOutputPath).Length / (1024 * 1024);
                Console.WriteLine($"[OnnxExporter] ONNX export complete: {onnxOutputPath} ({sizeMb} MB)");

                if (sizeMb > 10)
                    Console.WriteLine("[OnnxExporter] WARNING: model > 10 MB — apply INT8 quantisation: " +
                                      "python -m onnxruntime.tools.quantization.quantize_static " +
                                      "--input model.onnx --output model_int8.onnx");
                else
                    Console.WriteLine("[OnnxExporter] Model size OK (< 10 MB). No quantisation required.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnnxExporter] ONNX export not available for this pipeline: {ex.Message}");
                Console.WriteLine("[OnnxExporter] -> Use the ML.NET .zip file or apply Python conversion " +
                                  "(see README.md for instructions).");
            }
        }
    }
}
