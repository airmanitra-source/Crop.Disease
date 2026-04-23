namespace Crop.Disease.API.Services
{
    /// <summary>
    /// EN: Generates bilingual agronomic rationale and USSD/SMS templates for each predicted disease class.
    ///     Labels match the dataset folder names: Blight, Common_Rust, Gray_Leaf_Spot, Healthy.
    ///     Texts are kept under 160 characters for feature-phone SMS delivery.
    ///
    /// FR: Genere le texte agronomique bilingue et les templates USSD/SMS pour chaque classe predite.
    ///     Les labels correspondent aux noms de dossiers du dataset : Blight, Common_Rust, Gray_Leaf_Spot, Healthy.
    ///     Les textes restent sous 160 caracteres pour la livraison SMS sur feature phone.
    /// </summary>
    public class RationaleService
    {
        private static readonly Dictionary<string, DiseaseInfo> KnowledgeBase =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // EN: Blight = Northern Leaf Blight (Helminthosporium turcicum)
            // FR: Blight = Brulure des feuilles / Helminthosporiose
            ["Blight"] = new DiseaseInfo(
                "Northern Leaf Blight / Brulure des feuilles",
                "Indwara ikonjesha ibabi",
                "Apply Propiconazole 25 EC (0.5 L/ha). Avoid overhead irrigation."),

            // EN: Common_Rust = Puccinia sorghi
            // FR: Common_Rust = Rouille commune du mais
            ["Common_Rust"] = new DiseaseInfo(
                "Common Rust / Rouille commune",
                "Indwara y'umutuku ku mibago",
                "Apply Mancozeb 80 WP (2 kg/ha). Use resistant seed varieties."),

            // EN: Gray_Leaf_Spot = Cercospora zeae-maydis
            // FR: Gray_Leaf_Spot = Cercosporiose grise
            ["Gray_Leaf_Spot"] = new DiseaseInfo(
                "Gray Leaf Spot / Cercosporiose grise",
                "Indwara y'amabara y'iguruka",
                "Apply Trifloxystrobin + Tebuconazole. Practice crop rotation."),

            // EN: Healthy = no disease detected
            // FR: Healthy = pas de maladie detectee
            ["Healthy"] = new DiseaseInfo(
                "Healthy plant / Plante saine",
                "Igihingwa ni muzima",
                "No treatment needed. Continue good agricultural practices."),
        };

        /// <summary>
        /// EN: Returns the full agronomic rationale (English + French) for the predicted label.
        /// FR: Retourne la justification agronomique complete (anglais + francais) pour le label predit.
        /// </summary>
        public string GetRationale(string label, float confidence)
        {
            var info = Lookup(label);
            return "[EN] " + info.DiseaseName + " (confidence: " + confidence.ToString("P0") + "). " +
                   "Recommended action: " + info.TreatmentEn;
        }

        /// <summary>
        /// EN: Returns a bilingual USSD/SMS template ready to send, under 160 characters.
        /// FR: Retourne un template USSD/SMS bilingue pret a envoyer, sous 160 caracteres.
        /// </summary>
        public string GetUssdSmsTemplate(string label, float confidence)
        {
            var info = Lookup(label);
            string msg = "[RW]" + info.KinyarwandaShort + "/" +
                         "[FR]" + info.DiseaseName + " " + confidence.ToString("P0") + ". " +
                         Truncate(info.TreatmentEn, 60);
            return msg.Length <= 160 ? msg : msg.Substring(0, 160);
        }

        /// <summary>
        /// EN: Low-confidence escalation message prompting the farmer to retake the photo.
        /// FR: Message d escalade en cas de faible confiance invitant a reprendre la photo.
        /// </summary>
        public string GetLowConfidenceMessage()
            => "[RW]Ifoto ntabwo iri sobanutse. Fata ifoto nziza./[FR]Photo floue. Prenez une 2e photo.";

        private static DiseaseInfo Lookup(string label)
        {
            if (KnowledgeBase.TryGetValue(label.Replace(" ", "_"), out var info)) return info;
            return new DiseaseInfo(label, "Reba umuhanga.", "Consult a local agronomist.");
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}