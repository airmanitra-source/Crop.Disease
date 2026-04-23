namespace Crop.Disease.API.Models
{
    /// <summary>
    /// EN: A single ranked prediction entry in the Top-N output list.
    /// FR: Une entree de prediction classee dans la liste Top-N.
    /// </summary>
    public class TopPrediction
    {
        /// <summary>EN: Disease label name. / FR: Nom du label de maladie.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>EN: Softmax probability score [0,1]. / FR: Score de probabilite softmax [0,1].</summary>
        public float Score { get; set; }
    }
}