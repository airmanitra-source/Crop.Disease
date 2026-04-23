using Crop.Disease.API.Models;
using Crop.Disease.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Crop.Disease.API.Controllers
{
    /// <summary>
    /// Controller principal de classification de maladies du maïs.
    /// 
    /// Endpoint POST /predict :
    ///   - Accepte une image JPEG/PNG en multipart/form-data
    ///   - Retourne { label, confidence, top3, latency_ms, rationale, ussd_sms_template }
    /// 
    /// Workflow 3 étapes pour feature phone (Product & Business adaptation) :
    ///   1. Photo capturée localement (ou via agent terrain / kiosque coopérative)
    ///   2. Upload vers cet endpoint (même en 2G/EDGE)
    ///   3. Résultat retourné via USSD/SMS au numéro de l'agriculteur
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class PredictController : ControllerBase
    {
        private readonly InferenceService _inferenceService;
        private readonly RationaleService _rationaleService;
        private readonly ILogger<PredictController>  _logger;

        public PredictController(
            InferenceService inferenceService,
            RationaleService rationaleService,
            ILogger<PredictController> logger)
        {
            _inferenceService = inferenceService;
            _rationaleService = rationaleService;
            _logger           = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /predict
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Classifie une image de feuille de maïs et retourne le diagnostic.
        /// </summary>
        /// <param name="image">Fichier image JPEG ou PNG (max 5 MB).</param>
        /// <returns>PredictResponse avec label, confiance, top3, latence et rationale.</returns>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
        public IActionResult Post([Required] IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { error = "Aucune image fournie." });

            var allowed = new[] { "image/jpeg", "image/png", "image/jpg" };
            if (!allowed.Contains(image.ContentType.ToLower()))
                return BadRequest(new { error = "Format non supporté. Utilisez JPEG ou PNG." });

            try
            {
                PredictResponse response;
                using (var stream = image.OpenReadStream())
                {
                    response = _inferenceService.Predict(stream);
                }

                // ── Enrichissement Product & Business ─────────────────────────
                response.Rationale = _rationaleService.GetRationale(response.Label, response.Confidence);
                response.UssdSmsTemplate = response.LowConfidenceEscalation
                    ? _rationaleService.GetLowConfidenceMessage()
                    : _rationaleService.GetUssdSmsTemplate(response.Label, response.Confidence);

                _logger.LogInformation(
                    "Prédiction : {Label} ({Confidence:P1}) en {Latency}ms | LowConf={LowConf}",
                    response.Label, response.Confidence, response.LatencyMs, response.LowConfidenceEscalation);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'inférence");
                return StatusCode(500, new { error = "Erreur interne du serveur.", detail = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /predict/health
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Health-check : vérifie que le service est opérationnel.
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
            => Ok(new { status = "ok", model = "crop-disease-onnx", timestamp = DateTime.UtcNow });
    }
}
