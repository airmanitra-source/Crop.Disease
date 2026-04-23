namespace Crop.Disease.API.Services
{
    /// <summary>
    /// EN: Holds the agronomic knowledge for a single disease class:
    ///     display name, Kinyarwanda short text, and recommended treatment.
    /// FR: Contient les connaissances agronomiques d une classe de maladie :
    ///     nom affiche, texte court en Kinyarwanda et traitement recommande.
    /// </summary>
    public class DiseaseInfo
    {
        /// <summary>EN: Bilingual disease display name. / FR: Nom bilingue de la maladie.</summary>
        public string DiseaseName { get; }

        /// <summary>EN: Short Kinyarwanda text for SMS. / FR: Texte court en kinyarwanda pour SMS.</summary>
        public string KinyarwandaShort { get; }

        /// <summary>EN: Recommended treatment in English. / FR: Traitement recommande en anglais.</summary>
        public string TreatmentEn { get; }

        public DiseaseInfo(string diseaseName, string kinyarwandaShort, string treatmentEn)
        {
            DiseaseName = diseaseName;
            KinyarwandaShort = kinyarwandaShort;
            TreatmentEn = treatmentEn;
        }
    }
}