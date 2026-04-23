using Crop.Disease.API.Models;
using Crop.Disease.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Crop.Disease.API.Controllers
{
    /// <summary>
    /// EN: Fallback endpoint for farmers without camera access.
    ///     Accepts a free-text symptom description and returns a diagnosis or technician escalation.
    ///
    /// FR: Endpoint de fallback pour les agriculteurs sans accès à une caméra.
    ///     Accepte une description textuelle des symptômes et retourne un diagnostic ou une escalade technicien.
    ///
    /// Workflow :
    ///   1. Farmer calls *123# (USSD) or sends SMS → operator forwards text to POST /diagnose/symptoms
    ///   2. API matches keywords → known disease  → returns treatment + SMS template
    ///                          → ambiguous/unknown → schedules technician visit + confirms by SMS
    /// </summary>
    [ApiController]
    [Route("diagnose")]
    public class DiagnoseController : ControllerBase
    {
        private readonly SymptomMatcherService         _matcher;
        private readonly ILogger<DiagnoseController>   _logger;

        public DiagnoseController(SymptomMatcherService matcher, ILogger<DiagnoseController> logger)
        {
            _matcher = matcher;
            _logger  = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /diagnose/symptoms
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// EN: Diagnose maize disease from a text symptom description.
        ///     If symptoms are clear enough, returns the disease + treatment.
        ///     If ambiguous or unknown, schedules a technician visit.
        ///
        /// FR: Diagnostique une maladie du maïs à partir d'une description textuelle.
        ///     Si les symptômes sont suffisamment clairs, retourne la maladie + traitement.
        ///     Si ambigu ou inconnu, planifie une visite technicien.
        /// </summary>
        /// <param name="request">Symptom description + optional phone number.</param>
        [HttpPost("symptoms")]
        public IActionResult Symptoms([FromBody] SymptomRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
                return BadRequest(new { error = "Please describe your symptoms." });

            var response = _matcher.Diagnose(request);

            _logger.LogInformation(
                "Symptom diagnosis | Label={Label} | Confidence={Confidence} | Technician={Technician} | Phone={Phone}",
                response.Label ?? "unknown", response.ConfidenceLevel,
                response.TechnicianVisitScheduled, request.PhoneNumber ?? "-");

            return Ok(response);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /diagnose/symptoms/guide
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// EN: Returns a short guide to help farmers describe their symptoms.
        /// FR: Retourne un guide court pour aider les agriculteurs à décrire leurs symptômes.
        /// </summary>
        [HttpGet("symptoms/guide")]
        public IActionResult Guide() => Ok(new
        {
            instructions = "Describe what you see on your maize leaves. Examples:",
            examples = new[]
            {
                "long grey spots on the leaves",
                "red or orange powder on the leaves",
                "burnt leaves with dry edges",
                "green and normal leaves",
                "ibabi ririmba amabara y'umutuku (Kinyarwanda)",
            },
            tip = "The more precise your description, the better the diagnosis."
        });
    }
}
