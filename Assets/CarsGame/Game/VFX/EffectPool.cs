using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Pre-built cheap flashes (expanding unlit sphere, optional point light on desktop) for
    /// shots, impacts and wrecks. Nothing is allocated per effect: one shared material, one
    /// MaterialPropertyBlock per flash, objects are toggled, never created or destroyed.
    /// </summary>
    public sealed class EffectPool : MonoBehaviour
    {
        private static EffectPool instance;
        public static EffectPool Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<EffectPool>();
                    if (instance == null) instance = new GameObject("EffectPool").AddComponent<EffectPool>();
                }
                return instance;
            }
        }

        [Tooltip("Flashes available at once; the oldest is recycled when exhausted.")]
        public int poolSize = 16;
        [Tooltip("Point lights are skipped on mobile: too costly for the forward mobile renderer.")]
        public bool useLights = true;
        [Tooltip("Assigned in the scene to avoid Shader.Find in builds. Falls back to URP/Unlit at runtime.")]
        public Material flashMaterial;

        private sealed class Flash
        {
            public Transform Transform;
            public Renderer Renderer;
            public Light Light;
            public MaterialPropertyBlock Block;
            public float Time, Duration, MaxRadius, StartIntensity;
            public Color Color;
            public bool Active;
        }

        private readonly List<Flash> flashes = new List<Flash>();
        private int nextIndex;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            if (Application.isMobilePlatform) useLights = false;
            if (flashMaterial == null) flashMaterial = BuildFallbackMaterial();
            for (int i = 0; i < poolSize; i++) flashes.Add(Build(i));
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        private Flash Build(int i)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Flash_" + i;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = flashMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Light light = null;
            if (useLights)
            {
                light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
            }
            go.SetActive(false);
            return new Flash { Transform = go.transform, Renderer = r, Light = light, Block = new MaterialPropertyBlock() };
        }

        /// <summary>Play a flash. duration in seconds, radius in metres.</summary>
        public void PlayFlash(Vector3 position, Color color, float duration, float maxRadius, float lightRange = 6f, float lightIntensity = 4f)
        {
            if (flashes.Count == 0) return;
            var f = flashes[nextIndex];
            nextIndex = (nextIndex + 1) % flashes.Count;

            f.Time = 0f; f.Duration = Mathf.Max(0.05f, duration); f.MaxRadius = maxRadius; f.Color = color; f.StartIntensity = lightIntensity;
            f.Transform.position = position;
            f.Transform.localScale = Vector3.one * 0.3f;
            if (f.Light != null) { f.Light.color = color; f.Light.range = lightRange; f.Light.intensity = lightIntensity; }
            f.Active = true;
            f.Transform.gameObject.SetActive(true);
            ApplyColor(f, 1f);
        }

        public void PlayShellImpact(Vector3 position) => PlayFlash(position, new Color(1f, 0.55f, 0.1f), 0.25f, 0.9f, 4f, 3f);
        public void PlayMuzzle(Vector3 position) => PlayFlash(position, new Color(1f, 0.75f, 0.3f), 0.12f, 0.45f, 5f, 4f);
        public void PlayWreck(Vector3 position) => PlayFlash(position, new Color(1f, 0.55f, 0.1f), 0.6f, 3.5f, 15f, 8f);

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < flashes.Count; i++)
            {
                var f = flashes[i];
                if (!f.Active) continue;
                f.Time += dt;
                float t = Mathf.Clamp01(f.Time / f.Duration);
                f.Transform.localScale = Vector3.one * Mathf.Lerp(0.3f, f.MaxRadius, Mathf.Sqrt(t));
                if (f.Light != null) f.Light.intensity = Mathf.Lerp(f.StartIntensity, 0f, t);
                ApplyColor(f, 1f - t);
                if (t >= 1f) { f.Active = false; f.Transform.gameObject.SetActive(false); }
            }
        }

        private void ApplyColor(Flash f, float alpha)
        {
            var c = f.Color; c.a = alpha;
            f.Block.SetColor(BaseColorId, c);
            f.Block.SetColor(ColorId, c);
            f.Renderer.SetPropertyBlock(f.Block);
        }

        // TODO(M5): replace with a serialized material asset so nothing depends on Shader.Find in builds.
        private static Material BuildFallbackMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = "FX_Flash_Unlit" };
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }
    }
}
