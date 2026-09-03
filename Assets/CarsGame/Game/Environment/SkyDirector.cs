using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RacingGame.World
{
    /// <summary>
    /// Applies a <see cref="SkyPreset"/> to the scene: skybox material instance, sun light, linear fog,
    /// trilight ambient, a CPU-built reflection cubemap (metallic paint picks up the sky colour), global
    /// shader tints for mountains/lamps and the post-process overrides. The only code that touches
    /// RenderSettings. Works in Edit mode too (for preview screenshots), except the post-process part.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SkyDirector : MonoBehaviour
    {
        public Material skyMaterial;
        public Light sun;
        public Volume volume;
        [Tooltip("Menu / fallback look (Day).")] public SkyPreset defaultPreset;
        [Tooltip("Used for events that only set the legacy 'night' flag.")] public SkyPreset nightPreset;
        [Range(16, 128)] public int reflectionSize = 32;

        public static SkyDirector Instance { get; private set; }
        public SkyPreset Current { get; private set; }
        public event System.Action<SkyPreset> Applied;

        private Material skyInstance;
        private Cubemap reflection;
        private VolumeProfile profileInstance;
        private Color[] scratch;

        private static readonly int ZenithId = Shader.PropertyToID("_ZenithColor");
        private static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
        private static readonly int GroundId = Shader.PropertyToID("_GroundColor");
        private static readonly int HorizonExpId = Shader.PropertyToID("_HorizonExp");
        private static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
        private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
        private static readonly int SunGlowId = Shader.PropertyToID("_SunGlow");
        private static readonly int SunGlowPowerId = Shader.PropertyToID("_SunGlowPower");
        private static readonly int MoonDirId = Shader.PropertyToID("_MoonDir");
        private static readonly int MoonColorId = Shader.PropertyToID("_MoonColor");
        private static readonly int MoonSizeId = Shader.PropertyToID("_MoonSize");
        private static readonly int StarsId = Shader.PropertyToID("_StarsIntensity");
        // Globals read by StylizedLit (mountain fog override, lamp emission).
        public static readonly int GlobalHorizonId = Shader.PropertyToID("_CarsHorizonColor");
        public static readonly int GlobalMountainId = Shader.PropertyToID("_CarsMountainColor");
        public static readonly int GlobalLampsId = Shader.PropertyToID("_CarsLampsOn");

        private void Awake()
        {
            Instance = this;
            Prepare();
            if (defaultPreset != null) Apply(defaultPreset);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Release();
        }

        private void Release()
        {
            if (skyInstance != null) { if (Application.isPlaying) Destroy(skyInstance); else DestroyImmediate(skyInstance); skyInstance = null; }
            if (reflection != null) { if (Application.isPlaying) Destroy(reflection); else DestroyImmediate(reflection); reflection = null; }
        }

        private void Prepare()
        {
            if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) { sun = l; break; }
            if (volume == null) volume = FindFirstObjectByType<Volume>();
            if (skyInstance == null && skyMaterial != null) skyInstance = new Material(skyMaterial) { name = skyMaterial.name + " (instance)", hideFlags = HideFlags.HideAndDontSave };
            if (reflection == null)
                reflection = new Cubemap(reflectionSize, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.MipChain) { name = "SkyReflection", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            if (profileInstance == null && Application.isPlaying && volume != null) profileInstance = volume.profile;   // instanced copy, never the shared asset
        }

        /// <summary>Preset for an event: explicit sky, else the legacy night flag, else the default.</summary>
        public SkyPreset PresetFor(SkyPreset explicitPreset, bool night)
        {
            if (explicitPreset != null) return explicitPreset;
            if (night && nightPreset != null) return nightPreset;
            return defaultPreset;
        }

        public void ApplyDefault() { if (defaultPreset != null) Apply(defaultPreset); }

        public void Apply(SkyPreset p)
        {
            if (p == null) return;
            Prepare();
            Current = p;
            var sp = p.ToParams();

            if (skyInstance != null)
            {
                // SetVector: no colour-space conversion, values are explicitly linear.
                skyInstance.SetVector(ZenithId, sp.Zenith);
                skyInstance.SetVector(HorizonId, sp.Horizon);
                skyInstance.SetVector(GroundId, sp.Ground);
                skyInstance.SetFloat(HorizonExpId, sp.HorizonExp);
                skyInstance.SetVector(SunDirId, sp.SunDir);
                skyInstance.SetVector(SunColorId, sp.SunColor);
                skyInstance.SetFloat(SunSizeId, sp.SunSize1mCos);
                skyInstance.SetFloat(SunGlowId, sp.SunGlow);
                skyInstance.SetFloat(SunGlowPowerId, sp.SunGlowPower);
                skyInstance.SetVector(MoonDirId, sp.MoonDir);
                skyInstance.SetVector(MoonColorId, sp.MoonColor);
                skyInstance.SetFloat(MoonSizeId, sp.MoonSize1mCos);
                skyInstance.SetFloat(StarsId, p.stars);
                RenderSettings.skybox = skyInstance;
            }

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                sun.color = p.sunLightColor;
                sun.intensity = p.sunIntensity;
                RenderSettings.sun = sun;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sp.Horizon;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky.linear;
            RenderSettings.ambientEquatorColor = p.ambientEquator.linear;
            RenderSettings.ambientGroundColor = p.ambientGround.linear;

            if (reflection != null)
            {
                SkyMath.FillCubemap(reflection, sp, scratch);
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflection;
                RenderSettings.reflectionIntensity = p.reflectionIntensity;
            }

            Shader.SetGlobalVector(GlobalHorizonId, sp.Horizon);
            Shader.SetGlobalVector(GlobalMountainId, p.mountainColor.linear);
            Shader.SetGlobalFloat(GlobalLampsId, p.lampsOn ? 1f : 0f);

            ApplyPost(p);
            Applied?.Invoke(p);
        }

        private void ApplyPost(SkyPreset p)
        {
            if (profileInstance == null) return;
            if (profileInstance.TryGet(out ColorAdjustments ca))
            {
                ca.postExposure.Override(p.postExposure);
                ca.saturation.Override(p.saturation);
                ca.contrast.Override(p.contrast);
                ca.colorFilter.Override(p.colorFilter);
            }
            if (profileInstance.TryGet(out Bloom bloom)) bloom.intensity.Override(p.bloomIntensity);
            if (profileInstance.TryGet(out Vignette vig)) vig.intensity.Override(p.vignette);
        }
    }
}
