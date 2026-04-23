namespace Crop.Disease.API.Models
{
    /// <summary>
    /// EN: Unit economics metrics for a single diagnosis event.
    /// FR: Metriques d economie unitaire pour un evenement de diagnostic.
    /// </summary>
    public class UnitEconomics
    {
        /// <summary>EN: Estimated cost per diagnosis (USD). / FR: Cout estime par diagnostic (USD).</summary>
        public decimal CostPerDiagnosisUsd { get; set; } = 0.003m;

        /// <summary>EN: Estimated value of a saved maize harvest (USD/ha). / FR: Valeur estimee d une recolte sauvee (USD/ha).</summary>
        public decimal EstimatedCropValueSavedUsd { get; set; } = 250m;

        /// <summary>EN: ROI summary for 1000 farmers. / FR: Resume du ROI pour 1000 agriculteurs.</summary>
        public string Roi1000Farmers =>
            $"Cost: ${CostPerDiagnosisUsd * 1000:F2} | Estimated saved crop value: ${EstimatedCropValueSavedUsd * 1000:F2}";
    }
}