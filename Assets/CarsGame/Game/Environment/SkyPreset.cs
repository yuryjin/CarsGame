using UnityEngine;

namespace RacingGame.World
{
    /// <summary>
    /// One time-of-day look: sky gradient, sun/moon, fog, ambient, mountain tint, post tweaks.
    /// Colours are authored in sRGB (inspector); <see cref="SkyDirector"/> converts once when applying.
    /// </summary>
    [CreateAssetMenu(menuName = "Racing/Sky Preset", fileName = "Sky")]
    public sealed class SkyPreset : ScriptableObject
    {
        [Header("Sky")]
        public Color zenith = new Color(0.20f, 0.45f, 0.90f);
        public Color horizon = new Color(0.78f, 0.88f, 0.97f);
        public Color ground = new Color(0.30f, 0.33f, 0.36f);
        [Range(0.2f, 3f)] public float horizonExp = 0.55f;

        [Header("Sun (also the directional light)")]
        [Tooltip("Euler angles of the light. Keep elevation >= 20 deg so flat facets do not show shadow acne.")]
        public Vector3 sunEuler = new Vector3(45f, 325f, 0f);
        public Color sunLightColor = new Color(1f, 0.95f, 0.88f);
        [Range(0f, 3f)] public float sunIntensity = 1.4f;
        [ColorUsage(false, true)] public Color sunDiscColor = new Color(2.5f, 2.2f, 1.6f);
        [Range(0.2f, 6f)] public float sunAngularRadius = 1.4f;
        [Range(0f, 2f)] public float sunGlow = 0.35f;
        [Range(2f, 128f)] public float sunGlowPower = 24f;

        [Header("Moon / stars (night)")]
        public Vector3 moonEuler = new Vector3(40f, 120f, 0f);
        public Color moonColor = Color.black;
        [Range(0.2f, 6f)] public float moonAngularRadius = 1.6f;
        [Range(0f, 2f)] public float stars = 0f;

        [Header("Fog (linear, colour = horizon)")]
        public float fogStart = 120f;
        public float fogEnd = 420f;

        [Header("Ambient (trilight)")]
        public Color ambientSky = new Color(0.55f, 0.65f, 0.85f);
        public Color ambientEquator = new Color(0.55f, 0.55f, 0.55f);
        public Color ambientGround = new Color(0.20f, 0.18f, 0.15f);
        [Range(0f, 2f)] public float reflectionIntensity = 1f;

        [Header("Mountains / distant tint")]
        public Color mountainColor = new Color(0.55f, 0.62f, 0.75f);

        [Header("Post")]
        [Range(-2f, 2f)] public float postExposure = 0.15f;
        [Range(-100f, 100f)] public float saturation = 18f;
        [Range(-100f, 100f)] public float contrast = 14f;
        public Color colorFilter = Color.white;
        [Range(0f, 3f)] public float bloomIntensity = 0.7f;
        [Range(0f, 1f)] public float vignette = 0.28f;

        [Header("Gameplay hints")]
        public bool headlights;
        [Tooltip("Emissive lamp posts / light pools are visible.")]
        public bool lampsOn;

        public Vector3 SunDirection => Quaternion.Euler(sunEuler) * Vector3.back;   // towards the sun
        public Vector3 MoonDirection => Quaternion.Euler(moonEuler) * Vector3.back;

        public SkyParams ToParams() => new SkyParams
        {
            Zenith = zenith.linear, Horizon = horizon.linear, Ground = ground.linear, HorizonExp = horizonExp,
            SunDir = SunDirection, SunColor = sunDiscColor.linear, SunSize1mCos = SkyMath.DiscSize(sunAngularRadius),
            SunGlow = sunGlow, SunGlowPower = sunGlowPower,
            MoonDir = MoonDirection, MoonColor = moonColor.linear, MoonSize1mCos = SkyMath.DiscSize(moonAngularRadius)
        };
    }
}
