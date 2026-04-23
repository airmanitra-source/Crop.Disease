using Microsoft.ML.Data;

namespace Crop.Disease.Classifier.Models
{
    /// <summary>
    /// Contient la prédiction de maladie retournée par le pipeline ML.NET.
    /// </summary>
    public class ImagePrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        public float[] Score { get; set; } = Array.Empty<float>();
    }
}
