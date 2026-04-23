using Crop.Disease.API.Models;

namespace Crop.Disease.API.Services
{
    /// <summary>
    /// EN: Keyword-based symptom matcher for farmers without camera access.
    ///     Matches free-text descriptions (FR / EN / Kinyarwanda) to known disease profiles.
    ///     If no match is found with sufficient confidence, a technician visit is scheduled.
    ///
    /// FR: Correspondance de symptômes par mots-clés pour les agriculteurs sans caméra.
    ///     Associe des descriptions textuelles libres (FR / EN / Kinyarwanda) aux maladies connues.
    ///     Si aucune correspondance suffisante n'est trouvée, une visite technicien est planifiée.
    /// </summary>
    public class SymptomMatcherService
    {
        // ── Keyword profiles per disease ─────────────────────────────────────
        // Each list covers French, English and Kinyarwanda terms farmers typically use.
        private static readonly List<DiseaseProfile> Profiles = new()
        {
            new DiseaseProfile(
                label:       "Blight",
                displayName: "Northern Leaf Blight",
                treatment:   "Apply Propiconazole 25 EC (0.5 L/ha). Avoid overhead irrigation.",
                smsTemplate: "[RW]Indwara ikonjesha ibabi. Koresha Propiconazole./[EN]Northern Leaf Blight. Propiconazole 0.5L/ha.",
                keywords: new[]
                {
                    // French
                    "brulure","brûlure","brulee","brûlée","brule","bruler",
                    "tache longue","tache grise allongee","tache allongee","grise allongee",
                    "feuille seche","feuilles mortes","necroses",
                    // English
                    "blight","burn","burnt","brown lesion","long lesion","dead leaf",
                    "grey spot","gray spot","grey lesion","gray lesion",
                    // Kinyarwanda
                    "ikonjesha","gukoma","ibabi ryumye","amabara y'iguruka"
                }
            ),

            new DiseaseProfile(
                label:       "Common_Rust",
                displayName: "Common Rust",
                treatment:   "Apply Mancozeb 80 WP (2 kg/ha). Use resistant seed varieties.",
                smsTemplate: "[RW]Indwara y'umutuku. Koresha Mancozeb 2kg/ha./[EN]Common Rust. Mancozeb 2kg/ha.",
                keywords: new[]
                {
                    // French
                    "rouille","rouge","poudre rouge","poudre orange","pustule","tache rouge",
                    "tache orange","feuille rouge","feuilles rouges",
                    // English
                    "rust","red powder","orange powder","pustule","red spot","orange spot",
                    "reddish","rusty",
                    // Kinyarwanda
                    "umutuku","poda itukura","ibabi ritukura","intege z'umutuku"
                }
            ),

            new DiseaseProfile(
                label:       "Gray_Leaf_Spot",
                displayName: "Gray Leaf Spot",
                treatment:   "Apply Trifloxystrobin + Tebuconazole. Practice crop rotation.",
                smsTemplate: "[RW]Indwara y'amabara masha. Hindura imyaka./[EN]Gray Leaf Spot. Rotate crops.",
                keywords: new[]
                {
                    // French
                    "cercosporiose","gris","grise","tache rectangulaire","tache carree",
                    "tache angulaire","bordure jaune","halo jaune","feuille terne",
                    // English
                    "gray leaf","grey leaf","cercospora","rectangular spot","angular spot",
                    "yellow halo","dull leaf","ashy",
                    // Kinyarwanda
                    "ibara ry'ibicucu","amabara masha","amabara y'umusemburo","ibabi ry'ibicucu"
                }
            ),

            new DiseaseProfile(
                label:       "Healthy",
                displayName: "Healthy plant",
                treatment:   "No treatment needed. Continue good agricultural practices.",
                smsTemplate: "[RW]Igihingwa ni muzima. Komeza!/[EN]Plant is healthy. Keep it up.",
                keywords: new[]
                {
                    // French
                    "sain","saine","normal","normale","vert","verte","bonne sante","pas de maladie",
                    "rien","aucune tache","feuille verte",
                    // English
                    "healthy","normal","green","no disease","no spot","looks good","fine",
                    // Kinyarwanda
                    "muzima","nta ndwara","ibara ry'icyatsi","neza"
                }
            ),
        };

        /// <summary>
        /// EN: Matches a free-text symptom description to a disease profile.
        ///     Returns a diagnosis response with recommendation or technician escalation.
        /// FR: Fait correspondre une description textuelle à un profil de maladie.
        ///     Retourne une réponse de diagnostic avec recommandation ou escalade technicien.
        /// </summary>
        public SymptomDiagnosisResponse Diagnose(SymptomRequest request)
        {
            string input = Normalize(request.Description);

            // Score each disease profile by counting keyword matches
            var scores = Profiles
                .Select(p => (Profile: p, Score: p.Keywords.Count(k => input.Contains(k))))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ToList();

            // No match → escalate to technician
            if (scores.Count == 0)
                return Escalate(request);

            var best = scores[0];

            // Ambiguous: top two profiles have the same score → escalate
            if (scores.Count > 1 && scores[1].Score == best.Score)
                return Escalate(request);

            string level = best.Score >= 3 ? "High" : best.Score == 2 ? "Medium" : "Low";

            // Low confidence → still give the best guess but warn
            if (level == "Low")
            {
                return new SymptomDiagnosisResponse
                {
                    Label                    = best.Profile.Label,
                    ConfidenceLevel          = "Low",
                    Recommendation           = $"Probable diagnosis: {best.Profile.DisplayName}. " +
                                               $"Please describe more precisely or send a photo. " +
                                               $"Possible treatment: {best.Profile.Treatment}",
                    UssdSmsTemplate          = "[EN]Uncertain diagnosis. More details needed or technician dispatched." +
                                               (string.IsNullOrEmpty(request.PhoneNumber) ? "" : $" Tel:{request.PhoneNumber}"),
                    TechnicianVisitScheduled = false
                };
            }

            return new SymptomDiagnosisResponse
            {
                Label                    = best.Profile.Label,
                ConfidenceLevel          = level,
                Recommendation           = $"{best.Profile.DisplayName}. {best.Profile.Treatment}",
                UssdSmsTemplate          = best.Profile.SmsTemplate,
                TechnicianVisitScheduled = false
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static SymptomDiagnosisResponse Escalate(SymptomRequest request)
        {
            string phone = string.IsNullOrEmpty(request.PhoneNumber) ? "not provided" : request.PhoneNumber;
            return new SymptomDiagnosisResponse
            {
                Label                    = null,
                ConfidenceLevel          = "Unknown",
                Recommendation           = "Symptoms not recognised. An agricultural technician will visit you shortly.",
                UssdSmsTemplate          = $"[RW]Umuhanga azaza kwawe./[EN]Technician dispatched. Registered phone: {phone}",
                TechnicianVisitScheduled = true
            };
        }

        private static string Normalize(string input)
            => input.ToLowerInvariant()
                    .Replace("é", "e").Replace("è", "e").Replace("ê", "e")
                    .Replace("à", "a").Replace("â", "a")
                    .Replace("î", "i").Replace("ô", "o").Replace("û", "u")
                    .Replace("ç", "c");
    }

    // ── Internal DTO ─────────────────────────────────────────────────────────

    internal class DiseaseProfile
    {
        public string   Label       { get; }
        public string   DisplayName { get; }
        public string   Treatment   { get; }
        public string   SmsTemplate { get; }
        public string[] Keywords    { get; }

        public DiseaseProfile(string label, string displayName, string treatment,
                               string smsTemplate, string[] keywords)
        {
            Label       = label;
            DisplayName = displayName;
            Treatment   = treatment;
            SmsTemplate = smsTemplate;
            Keywords    = keywords;
        }
    }
}
