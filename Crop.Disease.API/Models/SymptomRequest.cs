namespace Crop.Disease.API.Models
{
    /// <summary>
    /// EN: Text-based symptom description submitted by a farmer without camera access.
    /// FR: Description textuelle des symptomes soumise par un agriculteur sans accès à une caméra.
    /// </summary>
    public class SymptomRequest
    {
        /// <summary>
        /// EN: Free-text symptom description in French, English, or Kinyarwanda.
        ///     Example: "feuilles avec taches grises allongées", "ibabi ririmba amabara y'umutuku"
        /// FR: Description libre des symptomes en français, anglais ou kinyarwanda.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// EN: Optional farmer phone number for technician callback.
        /// FR: Numéro de téléphone optionnel de l'agriculteur pour rappel technicien.
        /// </summary>
        public string? PhoneNumber { get; set; }
    }
}
