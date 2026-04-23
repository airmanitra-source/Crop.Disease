namespace Crop.Disease.Classifier.Services
{
    /// <summary>
    /// EN: Supported augmentation types applied by ImageAugmentor.
    ///     Each type simulates a real-world field condition degradation.
    /// FR: Types d augmentations supportes appliques par ImageAugmentor.
    ///     Chaque type simule une degradation de condition terrain reelle.
    /// </summary>
    public enum AugmentationType
    {
        /// <summary>EN: Additive Gaussian pixel noise (sigma=25). / FR: Bruit gaussien additif par pixel (sigma=25).</summary>
        GaussianNoise,

        /// <summary>EN: Motion blur with random direction to simulate handheld shake. / FR: Flou de mouvement directionnel simulant le tremblement.</summary>
        MotionBlur,

        /// <summary>EN: Random brightness factor [0.5, 1.5] to simulate daylight variation. / FR: Facteur de luminosite aleatoire [0.5, 1.5] pour la variation de lumiere.</summary>
        BrightnessJitter,

        /// <summary>EN: Combined brightness + saturation + hue rotation to simulate mixed lighting. / FR: Combinaison luminosite + saturation + teinte pour eclairage mixte.</summary>
        MixedLighting
    }
}