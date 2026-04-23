namespace Crop.Disease.API.Models
{
    /// <summary>
    /// EN: Inference result returned by the POST /predict endpoint.
    /// FR: Resultat d inference retourne par le endpoint POST /predict.
    /// </summary>
    public class PredictResponse
    {
        /// <summary>EN: Top predicted disease label. / FR: Label de maladie principal predit.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>EN: Softmax confidence score [0,1]. / FR: Score de confiance softmax [0,1].</summary>
        public float Confidence { get; set; }

        /// <summary>EN: Top-3 ranked predictions. / FR: Top-3 predictions classees.</summary>
        public List<TopPrediction> Top3 { get; set; } = new();

        /// <summary>EN: End-to-end inference latency (ms). / FR: Latence d inference de bout en bout (ms).</summary>
        public long LatencyMs { get; set; }

        /// <summary>EN: Agronomic rationale in Kinyarwanda + French. / FR: Justification agronomique KW + FR.</summary>
        public string Rationale { get; set; } = string.Empty;

        /// <summary>EN: Bilingual USSD/SMS template ready to send (< 160 chars). / FR: Template USSD/SMS bilingue pret a envoyer.</summary>
        public string UssdSmsTemplate { get; set; } = string.Empty;

        /// <summary>EN: True when confidence is below threshold — prompts a second photo. / FR: Vrai si confiance sous le seuil — invite a une 2eme photo.</summary>
        public bool LowConfidenceEscalation { get; set; }

        /// <summary>EN: Unit economics for this diagnosis event. / FR: Economie unitaire pour cet evenement de diagnostic.</summary>
        public UnitEconomics UnitEconomics { get; set; } = new();
    }
}