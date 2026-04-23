using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Crop.Disease.API.Models;
using System.Diagnostics;

namespace Crop.Disease.API.Services
{
    /// <summary>
    /// EN: Dual-mode inference service.
    ///     - ONNX INT8 (preferred, ~3 MB) via OnnxRuntime when model_int8.onnx is present.
    ///     - ML.NET model.zip (fallback, 22 MB) via PredictionEngine otherwise.
    /// FR: Service d inference en double mode.
    ///     - ONNX INT8 (prefere, ~3 Mo) via OnnxRuntime si model_int8.onnx est present.
    ///     - ML.NET model.zip (fallback, 22 Mo) via PredictionEngine sinon.
    /// </summary>
    public class InferenceService : IDisposable
    {
        private const float LowConfidenceThreshold = 0.60f;
        private const int   ImageSize              = 224;

        private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
        private static readonly float[] Std  = { 0.229f, 0.224f, 0.225f };

        private readonly bool      _useOnnx;
        private readonly string[]  _labels;

        // ONNX mode
        private readonly InferenceSession? _session;

        // ML.NET mode
        private readonly MLContext?                                   _mlContext;
        private readonly PredictionEngine<ImageInput, ImageOutput>?  _engine;

        public InferenceService(string modelPath, string labelsPath, bool useOnnx)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model not found: {modelPath}");
            if (!File.Exists(labelsPath))
                throw new FileNotFoundException($"Labels file not found: {labelsPath}");

            _useOnnx = useOnnx;
            _labels  = File.ReadAllLines(labelsPath)
                           .Where(l => !string.IsNullOrWhiteSpace(l))
                           .ToArray();

            if (useOnnx)
            {
                _session = new InferenceSession(modelPath);
                Console.WriteLine($"[InferenceService] ONNX INT8 loaded: {modelPath} ({new FileInfo(modelPath).Length / 1024} KB) | Labels: {_labels.Length}");
            }
            else
            {
                _mlContext = new MLContext(seed: 42);
                var model  = _mlContext.Model.Load(modelPath, out _);
                _engine    = _mlContext.Model.CreatePredictionEngine<ImageInput, ImageOutput>(model);
                Console.WriteLine($"[InferenceService] ML.NET model.zip loaded: {modelPath} ({new FileInfo(modelPath).Length / 1024} KB) | Labels: {_labels.Length}");
            }
        }

        public PredictResponse Predict(Stream imageStream)
        {
            return _useOnnx
                ? PredictOnnx(imageStream)
                : PredictMlNet(imageStream);
        }

        // -- ONNX path --------------------------------------------------------

        private PredictResponse PredictOnnx(Stream imageStream)
        {
            var sw     = Stopwatch.StartNew();
            var tensor = PreprocessImage(imageStream);

            string inputName = _session!.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            float[] scores;
            using (var results = _session.Run(inputs))
                scores = Softmax(results.First().AsEnumerable<float>().ToArray());

            sw.Stop();
            return BuildResponse(scores, sw.ElapsedMilliseconds);
        }

        private DenseTensor<float> PreprocessImage(Stream stream)
        {
            using var image = Image.Load<Rgb24>(stream);
            image.Mutate(x => x.Resize(ImageSize, ImageSize));

            var tensor = new DenseTensor<float>(new[] { 1, 3, ImageSize, ImageSize });
            for (int y = 0; y < ImageSize; y++)
                for (int x = 0; x < ImageSize; x++)
                {
                    var p = image[x, y];
                    tensor[0, 0, y, x] = (p.R / 255f - Mean[0]) / Std[0];
                    tensor[0, 1, y, x] = (p.G / 255f - Mean[1]) / Std[1];
                    tensor[0, 2, y, x] = (p.B / 255f - Mean[2]) / Std[2];
                }
            return tensor;
        }

        private static float[] Softmax(float[] logits)
        {
            float max = logits.Max();
            float[] exp = logits.Select(l => MathF.Exp(l - max)).ToArray();
            float sum   = exp.Sum();
            return exp.Select(e => e / sum).ToArray();
        }

        // -- ML.NET path ------------------------------------------------------

        private PredictResponse PredictMlNet(Stream imageStream)
        {
            var sw  = Stopwatch.StartNew();
            string tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
            try
            {
                using (var fs = File.Create(tmp))
                    imageStream.CopyTo(fs);

                var input  = new ImageInput { ImagePath = tmp, Label = string.Empty };
                var output = _engine!.Predict(input);
                sw.Stop();

                return BuildResponse(output.Score ?? Array.Empty<float>(), sw.ElapsedMilliseconds,
                                     output.PredictedLabel);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        // -- Shared response builder ------------------------------------------

        private PredictResponse BuildResponse(float[] scores, long latencyMs, string? fallbackLabel = null)
        {
            var ranked = scores
                .Select((s, i) => (Label: i < _labels.Length ? _labels[i] : $"class_{i}", Score: s))
                .OrderByDescending(x => x.Score)
                .Take(3)
                .ToList();

            float  topScore = ranked.Count > 0 ? ranked[0].Score : 0f;
            string topLabel = ranked.Count > 0 ? ranked[0].Label : fallbackLabel ?? "unknown";
            bool   lowConf  = topScore < LowConfidenceThreshold;

            return new PredictResponse
            {
                Label                   = topLabel,
                Confidence              = topScore,
                Top3                    = ranked.Select(r => new TopPrediction { Label = r.Label, Score = r.Score }).ToList(),
                LatencyMs               = latencyMs,
                LowConfidenceEscalation = lowConf,
                Rationale               = string.Empty,
                UssdSmsTemplate         = string.Empty,
                UnitEconomics           = new UnitEconomics()
            };
        }

        public void Dispose()
        {
            _session?.Dispose();
            _engine?.Dispose();
        }
    }

    // -- DTOs ML.NET internes -------------------------------------------------

    internal class ImageInput
    {
        [LoadColumn(0)] public string ImagePath { get; set; } = string.Empty;
        [LoadColumn(1)] public string Label     { get; set; } = string.Empty;
    }

    internal class ImageOutput
    {
        [ColumnName("PredictedLabel")] public string PredictedLabel { get; set; } = string.Empty;
        public float[] Score { get; set; } = Array.Empty<float>();
    }
}
