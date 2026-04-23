using Microsoft.ML.Data;

namespace Crop.Disease.Classifier.Models
{
    /// <summary>
    /// Représente une image d'entrée pour l'entraînement ML.NET.
    /// </summary>
    public class ImageData
    {
        [LoadColumn(0)]
        public string ImagePath { get; set; } = string.Empty;

        [LoadColumn(1)]
        public string Label { get; set; } = string.Empty;
    }
}
