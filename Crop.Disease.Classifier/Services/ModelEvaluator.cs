using Microsoft.ML;
using Microsoft.ML.Data;
using Crop.Disease.Classifier.Models;

namespace Crop.Disease.Classifier.Services
{
    /// <summary>
    /// Évalue un modèle entraîné sur un jeu de test et affiche les métriques principales.
    /// </summary>
    public class ModelEvaluator
    {
        private readonly MLContext _mlContext;

        public ModelEvaluator(MLContext mlContext)
        {
            _mlContext = mlContext;
        }

        /// <summary>
        /// Évalue le modèle et retourne les métriques multiclasses.
        /// </summary>
        public MulticlassClassificationMetrics Evaluate(
            ITransformer model,
            IEnumerable<ImageData> testData)
        {
            IDataView testView = _mlContext.Data.LoadFromEnumerable(testData);
            var predictions   = model.Transform(testView);
            var metrics       = _mlContext.MulticlassClassification.Evaluate(
                                    predictions, labelColumnName: "LabelKey");

            Console.WriteLine("─── Rapport d'évaluation ───────────────────────────────");
            Console.WriteLine($"  MicroAccuracy  : {metrics.MicroAccuracy:P2}");
            Console.WriteLine($"  MacroAccuracy  : {metrics.MacroAccuracy:P2}");
            Console.WriteLine($"  LogLoss        : {metrics.LogLoss:F4}");
            Console.WriteLine($"  LogLossReduction: {metrics.LogLossReduction:F4}");
            Console.WriteLine("────────────────────────────────────────────────────────");

            return metrics;
        }
    }
}
