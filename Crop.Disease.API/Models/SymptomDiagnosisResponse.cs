namespace Crop.Disease.API.Models
{
    /// <summary>
    /// EN: Response to a text-based symptom diagnosis request.
    /// FR: Réponse à une demande de diagnostic basée sur des symptomes textuels.
    /// </summary>
    public class SymptomDiagnosisResponse
    {
        /// <summary>EN: Predicted disease label, or null if unrecognised. / FR: Label prédit, ou null si non reconnu.</summary>
        public string? Label { get; set; }

        /// <summary>EN: Confidence level: High / Medium / Low / Unknown. / FR: Niveau de confiance : High / Medium / Low / Unknown.</summary>
        public string ConfidenceLevel { get; set; } = string.Empty;

        /// <summary>EN: Recommended treatment or next action. / FR: Traitement recommandé ou prochaine action.</summary>
        public string Recommendation { get; set; } = string.Empty;

        /// <summary>EN: Bilingual USSD/SMS template (< 160 chars). / FR: Template USSD/SMS bilingue (< 160 car.).</summary>
        public string UssdSmsTemplate { get; set; } = string.Empty;

        /// <summary>EN: True when a technician visit is scheduled. / FR: Vrai quand une visite technicien est planifiée.</summary>
        public bool TechnicianVisitScheduled { get; set; }
    }
}
