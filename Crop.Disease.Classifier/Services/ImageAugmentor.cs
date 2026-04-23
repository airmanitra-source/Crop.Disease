using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace Crop.Disease.Classifier.Services
{
    /// <summary>
    /// EN: Applies photometric and geometric augmentations to simulate real-world field conditions:
    ///     Gaussian noise, motion blur, brightness jitter, and mixed lighting.
    ///     Used to generate the 60-image robustness evaluation set (test_field equivalent).
    ///
    /// FR: Applique des augmentations photométriques et géométriques pour simuler les conditions
    ///     terrain réelles : bruit gaussien, flou de mouvement, variation de luminosité et éclairage mixte.
    ///     Utilisé pour générer le jeu d'évaluation de robustesse de 60 images (équivalent test_field).
    /// </summary>
    public class ImageAugmentor
    {
        private readonly Random _rng;

        /// <param name="seed">
        /// EN: Random seed for reproducibility.
        /// FR: Graine aléatoire pour la reproductibilité.
        /// </param>
        public ImageAugmentor(int seed = 42)
        {
            _rng = new Random(seed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EN: Public API — apply a named augmentation to a file and save result.
        // FR: API publique — applique une augmentation nommée sur un fichier image.
        // ─────────────────────────────────────────────────────────────────────


        /// <summary>
        /// EN: Applies the requested augmentation to the source image, resizes to 224×224,
        ///     and saves the result to <paramref name="outputPath"/>.
        /// FR: Applique l'augmentation demandée sur l'image source, redimensionne à 224×224,
        ///     et sauvegarde le résultat dans <paramref name="outputPath"/>.
        /// </summary>
        public void Augment(string sourcePath, string outputPath, AugmentationType type)
        {
            using var image = Image.Load<Rgb24>(sourcePath);

            // Always resize to 224×224 first (training / inference consistency)
            // Toujours redimensionner à 224×224 en premier (cohérence entraînement/inférence)
            image.Mutate(x => x.Resize(224, 224));

            switch (type)
            {
                case AugmentationType.GaussianNoise:
                    ApplyGaussianNoise(image, sigma: 25f);
                    break;
                case AugmentationType.MotionBlur:
                    ApplyMotionBlur(image);
                    break;
                case AugmentationType.BrightnessJitter:
                    ApplyBrightnessJitter(image);
                    break;
                case AugmentationType.MixedLighting:
                    ApplyMixedLighting(image);
                    break;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            image.Save(outputPath);
        }

        /// <summary>
        /// EN: Generates a complete 60-image noisy evaluation set (15 images × 4 augmentation types)
        ///     stratified across all label classes.
        ///     Mirrors the test_field.zip spec: motion blur + mixed lighting + Gaussian noise.
        /// FR: Génère un jeu d'évaluation bruité de 60 images (15 images × 4 types d'augmentation)
        ///     distribué sur toutes les classes de labels.
        ///     Correspond à la spec test_field.zip : flou de mouvement + éclairage mixte + bruit gaussien.
        /// </summary>
        /// <param name="allImages">
        /// EN: Full dataset image list to sample from.
        /// FR: Liste complète des images du dataset depuis lesquelles échantillonner.
        /// </param>
        /// <param name="outputDir">
        /// EN: Destination folder. Augmented images are written as PNG files.
        /// FR: Dossier de destination. Les images augmentées sont écrites en PNG.
        /// </param>
        /// <returns>
        /// EN: List of (augmented image path, label) pairs ready for evaluation.
        /// FR: Liste de paires (chemin image augmentée, label) prêtes pour l'évaluation.
        /// </returns>
        public List<(string Path, string Label)> GenerateEvalSet(
            IReadOnlyList<(string Path, string Label)> allImages,
            string outputDir)
        {
            const int TotalEvalImages       = 60;
            int       augTypeCount          = Enum.GetValues<AugmentationType>().Length; // 4
            int       imagesPerAugmentation = TotalEvalImages / augTypeCount;            // 15

            Directory.CreateDirectory(outputDir);

            // EN: Stratify by label so every class is represented.
            // FR: Stratification par label pour représenter toutes les classes.
            var byLabel = allImages
                .GroupBy(x => x.Label)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<(string Path, string Label)>();

            foreach (AugmentationType augType in Enum.GetValues<AugmentationType>())
            {
                // EN: Pick imagesPerAugmentation samples spread across all classes.
                // FR: Sélectionner imagesPerAugmentation échantillons répartis sur toutes les classes.
                var candidates = StratifiedSample(byLabel, imagesPerAugmentation);

                foreach (var (srcPath, label) in candidates)
                {
                    string fileName   = $"{label}_{augType}_{Path.GetFileNameWithoutExtension(srcPath)}.png";
                    string outputPath = Path.Combine(outputDir, label, fileName);

                    try
                    {
                        Augment(srcPath, outputPath, augType);
                        result.Add((outputPath, label));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ImageAugmentor] WARNING: skipped {srcPath} ({augType}): {ex.Message}");
                    }
                }

                Console.WriteLine($"[ImageAugmentor] {augType}: {imagesPerAugmentation} augmented images saved to {outputDir}");
            }

            Console.WriteLine($"[ImageAugmentor] Eval set complete: {result.Count} images total under {outputDir}");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // EN: Also used during training: random single augmentation on a single image stream.
        // FR: Aussi utilisé pendant l'entraînement : augmentation aléatoire unique sur un flux image.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// EN: Applies a randomly chosen augmentation to a raw image byte array and returns the result.
        ///     Input and output are always 224×224 PNG bytes.
        /// FR: Applique une augmentation aléatoire sur un tableau d'octets image bruts et retourne le résultat.
        ///     Entrée et sortie sont toujours en PNG 224×224.
        /// </summary>
        public byte[] AugmentBytes(byte[] rawImageBytes)
        {
            using var ms    = new MemoryStream(rawImageBytes);
            using var image = Image.Load<Rgb24>(ms);

            image.Mutate(x => x.Resize(224, 224));

            var type = (AugmentationType)_rng.Next(0, Enum.GetValues<AugmentationType>().Length);
            switch (type)
            {
                case AugmentationType.GaussianNoise:    ApplyGaussianNoise(image, sigma: 20f); break;
                case AugmentationType.MotionBlur:       ApplyMotionBlur(image);                break;
                case AugmentationType.BrightnessJitter: ApplyBrightnessJitter(image);           break;
                case AugmentationType.MixedLighting:    ApplyMixedLighting(image);              break;
            }

            using var outMs = new MemoryStream();
            image.SaveAsPng(outMs);
            return outMs.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private augmentation implementations
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// EN: Adds per-pixel Gaussian noise with the given standard deviation.
        /// FR: Ajoute du bruit gaussien par pixel avec l'écart-type donné.
        /// </summary>
        private void ApplyGaussianNoise(Image<Rgb24> image, float sigma)
        {
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x] = new Rgb24(
                            ClampByte(row[x].R + SampleGaussian(sigma)),
                            ClampByte(row[x].G + SampleGaussian(sigma)),
                            ClampByte(row[x].B + SampleGaussian(sigma)));
                    }
                }
            });
        }

        /// <summary>
        /// EN: Simulates motion blur using ImageSharp's built-in Gaussian blur with a horizontal kernel.
        ///     Rotation randomises the blur direction ±30 degrees to replicate handheld shake.
        /// FR: Simule un flou de mouvement avec le flou gaussien d'ImageSharp et un noyau horizontal.
        ///     La rotation aléatoire (±30°) simule le tremblement d'un appareil tenu à la main.
        /// </summary>
        private void ApplyMotionBlur(Image<Rgb24> image)
        {
            float angle = (float)(_rng.NextDouble() * 60 - 30); // ±30 degrees / ±30 degrés
            int   radius = _rng.Next(3, 8);

            image.Mutate(ctx =>
            {
                ctx.Rotate(angle)
                   .GaussianBlur(radius)
                   .Rotate(-angle)
                   .Resize(224, 224); // Restore size after rotation / Restaurer la taille après rotation
            });
        }

        /// <summary>
        /// EN: Randomly adjusts brightness in the range [0.5 – 1.5] to simulate varying daylight.
        /// FR: Ajuste aléatoirement la luminosité dans la plage [0.5 – 1.5] pour simuler la lumière variable.
        /// </summary>
        private void ApplyBrightnessJitter(Image<Rgb24> image)
        {
            float factor = 0.5f + (float)_rng.NextDouble(); // [0.5 – 1.5]
            image.Mutate(ctx => ctx.Brightness(factor));
        }

        /// <summary>
        /// EN: Simulates mixed lighting by combining brightness jitter with a hue rotation,
        ///     replicating warm-lamp / overcast-sky colour casts common in field photography.
        /// FR: Simule un éclairage mixte en combinant variation de luminosité et rotation de teinte,
        ///     reproduisant les dominantes colorées (lampe chaude / ciel couvert) fréquentes sur le terrain.
        /// </summary>
        private void ApplyMixedLighting(Image<Rgb24> image)
        {
            float brightness  = 0.6f + (float)_rng.NextDouble() * 0.8f;  // [0.6 – 1.4]
            float saturation  = 0.7f + (float)_rng.NextDouble() * 0.6f;  // [0.7 – 1.3]
            float hueRotation = (float)(_rng.NextDouble() * 40 - 20);    // ±20 degrees

            image.Mutate(ctx =>
                ctx.Brightness(brightness)
                   .Saturate(saturation)
                   .Hue(hueRotation));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Utilities
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// EN: Box-Muller transform for Gaussian sampling.
        /// FR: Transformée de Box-Muller pour l'échantillonnage gaussien.
        /// </summary>
        private float SampleGaussian(float sigma)
        {
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            double z  = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return (float)(z * sigma);
        }

        private static byte ClampByte(float val)
            => (byte)Math.Clamp((int)Math.Round(val), 0, 255);

        /// <summary>
        /// EN: Stratified sampling: picks <paramref name="total"/> items balanced across label groups.
        /// FR: Échantillonnage stratifié : sélectionne <paramref name="total"/> éléments équilibrés par classe.
        /// </summary>
        private List<(string Path, string Label)> StratifiedSample(
            Dictionary<string, List<(string Path, string Label)>> byLabel,
            int total)
        {
            int perClass  = Math.Max(1, total / byLabel.Count);
            var selection = new List<(string, string)>();

            foreach (var (label, items) in byLabel)
            {
                var shuffled = items.OrderBy(_ => _rng.Next()).Take(perClass).ToList();
                selection.AddRange(shuffled);
            }

            // EN: Trim or fill to exactly `total` if class count doesn't divide evenly.
            // FR: Ajuster à exactement `total` si les classes ne se divisent pas également.
            while (selection.Count > total) selection.RemoveAt(selection.Count - 1);
            while (selection.Count < total && byLabel.Any())
            {
                var fallback = byLabel.Values
                    .SelectMany(x => x)
                    .OrderBy(_ => _rng.Next())
                    .First();
                selection.Add(fallback);
            }

            return selection;
        }
    }
}
