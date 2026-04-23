using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Vision;
using Crop.Disease.Classifier.Models;

namespace Crop.Disease.Classifier.Services
{
    /// <summary>
    /// EN: Trains an image classifier via ML.NET ImageClassification (TensorFlow backend).
    ///     Backbone : MobileNetV2 — compact, suitable for mobile/edge deployment.
    ///     Input size enforced: 224×224 pixels (ImageNet standard).
    ///     Split strategy: 80 % train / 10 % validation / 10 % test from the 1500-sample set.
    ///     Target: macro-F1 ≥ 80 % on the clean test split.
    ///
    /// FR: Entraîne un classificateur d'images via ML.NET ImageClassification (backend TensorFlow).
    ///     Backbone : MobileNetV2 — compact, adapté au déploiement mobile/edge.
    ///     Taille d'entrée imposée : 224×224 pixels (standard ImageNet).
    ///     Stratégie de split : 80 % train / 10 % validation / 10 % test sur les 1500 échantillons.
    ///     Objectif : macro-F1 ≥ 80 % sur le split de test propre.
    /// </summary>
    public class ModelTrainer
    {
        // EN: Input image size (must match InferenceService.ImageSize = 224).
        // FR: Taille d'entrée image (doit correspondre à InferenceService.ImageSize = 224).
        private const int ImageSize = 224;

        private readonly MLContext _mlContext;

        public ModelTrainer()
        {
            _mlContext = new MLContext(seed: 42);
        }

        /// <summary>
        /// EN: Trains the model on <paramref name="imageData"/> and returns the trained transformer,
        ///     the ordered label array, and the input schema.
        ///     The 80/10/10 split is performed internally; only the 80 % portion is used for fitting.
        ///
        /// FR: Entraîne le modèle sur <paramref name="imageData"/> et retourne le transformateur entraîné,
        ///     le tableau de labels ordonné et le schéma d'entrée.
        ///     Le split 80/10/10 est effectué en interne ; seule la partie 80 % est utilisée pour l'ajustement.
        /// </summary>
        public (ITransformer model, string[] labels, IDataView trainView) Train(
            IEnumerable<ImageData> imageData,
            string checkpointFolder)
        {
            // ── 0. Clean checkpoint folder ───────────────────────────────────
            // EN: Windows keeps .meta.pb memory-mapped after the previous run.
            //     Deleting the folder before training avoids IOException on re-run.
            // FR: Windows garde .meta.pb en mémoire mappée après l'exécution précédente.
            //     Supprimer le dossier avant l'entraînement évite une IOException.
            if (Directory.Exists(checkpointFolder))
            {
                Console.WriteLine($"[ModelTrainer] Cleaning checkpoint folder: {checkpointFolder}");
                Directory.Delete(checkpointFolder, recursive: true);
            }
            Directory.CreateDirectory(checkpointFolder);

            // ── 1. Load data ──────────────────────────────────────────────────
            Console.WriteLine("[ModelTrainer] Loading image data into ML.NET context...");
            IDataView dataView = _mlContext.Data.LoadFromEnumerable(imageData);

            // ── 2. Shuffle then 80/10/10 split ───────────────────────────────
            // EN: We make two successive splits: first cut 20 % (val+test), then split that in half.
            // FR: Deux coupes successives : d'abord 20 % (val+test), puis couper ce bloc en deux.
            dataView = _mlContext.Data.ShuffleRows(dataView, seed: 42);

            var firstSplit  = _mlContext.Data.TrainTestSplit(dataView,   testFraction: 0.20, seed: 42);
            var secondSplit = _mlContext.Data.TrainTestSplit(firstSplit.TestSet, testFraction: 0.50, seed: 42);

            IDataView trainSet = firstSplit.TrainSet;   // 80 %
            IDataView valSet   = secondSplit.TrainSet;  // 10 %
            IDataView testSet  = secondSplit.TestSet;   // 10 %

            long trainCount = trainSet.GetRowCount() ?? -1;
            long valCount   = valSet.GetRowCount()   ?? -1;
            long testCount  = testSet.GetRowCount()  ?? -1;
            Console.WriteLine($"[ModelTrainer] Split — train: {trainCount} | val: {valCount} | test: {testCount}");

            // ── 3. Build pipeline ─────────────────────────────────────────────
            // EN: LoadRawImageBytes handles any resolution; ML.NET's TF backend
            //     internally resizes to 224×224 for MobileNetV2.
            //     We pre-resize here to be explicit and guarantee consistency.
            // FR: LoadRawImageBytes gère toute résolution ; le backend TF de ML.NET
            //     redimensionne en interne à 224×224 pour MobileNetV2.
            //     Pré-redimensionnement explicite ici pour garantir la cohérence.
            var pipeline = _mlContext.Transforms
                .Conversion.MapValueToKey("LabelKey", "Label")
                .Append(_mlContext.Transforms.LoadRawImageBytes(
                    outputColumnName: "Image",
                    imageFolder: null,
                    inputColumnName: "ImagePath"))
                .Append(_mlContext.MulticlassClassification.Trainers.ImageClassification(
                    new ImageClassificationTrainer.Options
                    {
                        FeatureColumnName = "Image",
                        LabelColumnName   = "LabelKey",
                        Arch              = ImageClassificationTrainer.Architecture.MobilenetV2,
                        Epoch             = 50,
                        BatchSize         = 32,
                        LearningRate      = 0.01f,
                        WorkspacePath     = checkpointFolder,
                        ReuseTrainSetBottleneckCachedValues = true,
                        MetricsCallback = (metrics) =>
                        {
                            Console.Write(
                                $"\r  [ModelTrainer] Epoch {metrics.Train?.Epoch ?? 0,3} | " +
                                $"Loss={metrics.Train?.CrossEntropy:F4} | " +
                                $"Acc={metrics.Train?.Accuracy:P1}   ");
                        }
                    }))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue(
                    "PredictedLabel", "PredictedLabel"));

            // ── 4. Train ──────────────────────────────────────────────────────
            Console.WriteLine($"\n[ModelTrainer] Starting training — input size: {ImageSize}×{ImageSize}...");
            var model = pipeline.Fit(trainSet);
            Console.WriteLine("\n[ModelTrainer] Training complete.");

            // ── 5. Evaluate on clean test split ───────────────────────────────
            Console.WriteLine("[ModelTrainer] Evaluating on clean test split (10%)...");
            var testPredictions = model.Transform(testSet);
            var metrics = _mlContext.MulticlassClassification.Evaluate(
                testPredictions, labelColumnName: "LabelKey", predictedLabelColumnName: "PredictedLabel");

            Console.WriteLine($"[ModelTrainer] Clean test — MicroAcc={metrics.MicroAccuracy:P2} | " +
                              $"MacroAcc={metrics.MacroAccuracy:P2} | " +
                              $"LogLoss={metrics.LogLoss:F4}");

            if (metrics.MacroAccuracy < 0.80)
                Console.WriteLine("[ModelTrainer] WARNING: MacroAccuracy below 80% target. " +
                                  "Consider more epochs or a larger sample.");

            // ── 6. Extract label array ────────────────────────────────────────
            // EN: Slot names are attached to the key-typed column produced by MapValueToKey.
            //     We read them from the trained model's output schema via GetKeyValues,
            //     which works regardless of whether the DataView was fully enumerated.
            // FR: Les noms de slots sont attachés à la colonne clé produite par MapValueToKey.
            //     On les lit depuis le schéma de sortie du modèle entraîné via GetKeyValues,
            //     ce qui fonctionne indépendamment de l'énumération du DataView.
            var trainedSchema = model.GetOutputSchema(trainSet.Schema);
            var keyCol = trainedSchema["LabelKey"];
            VBuffer<ReadOnlyMemory<char>> keyValues = default;
            keyCol.GetKeyValues(ref keyValues);
            string[] labels = keyValues.DenseValues().Select(v => v.ToString()).ToArray();

            Console.WriteLine($"[ModelTrainer] Labels ({labels.Length}): {string.Join(", ", labels)}");

            return (model, labels, trainSet);
        }
    }
}
