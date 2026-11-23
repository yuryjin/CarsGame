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
        public static EffectPool Current => instance;
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
            // WebGL on a phone reports isMobilePlatform; the device type covers browsers that do not.
            if (Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld) useLights = false;
            if (flashMaterial == null) flashMaterial = BuildFallbackMaterial();
            if (flashMaterial != null) for (int i = 0; i < poolSize; i++) flashes.Add(Build(i));   // no material: no flashes, never a magenta sphere
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
        public void PlayFlash(Vector3 position, Color color, float duration, float maxRadius, float lightRange = 6f, float lightIntensity = 4f, bool combat = true)
        {
            if (flashes.Count == 0) return;
            var f = flashes[nextIndex];
            nextIndex = (nextIndex + 1) % flashes.Count;

            float intensity = combat && PlatformGlue.Save?.Data.settings.reducedCombatFlashes == true ? 0.38f : 1f;
            f.Time = 0f; f.Duration = Mathf.Max(0.05f, duration); f.MaxRadius = maxRadius;
            f.Color = color * intensity; f.StartIntensity = lightIntensity * intensity;
            f.Transform.position = position;
            f.Transform.localScale = Vector3.one * 0.3f;
            if (f.Light != null) { f.Light.color = color; f.Light.range = lightRange; f.Light.intensity = f.StartIntensity; }
            f.Active = true;
            f.Transform.gameObject.SetActive(true);
            ApplyColor(f, 1f);
        }

        public void PlayShellImpact(Vector3 position)
        {
            PlayFlash(position, new Color(1f, 0.55f, 0.1f), 0.25f, 0.9f, 4f, 3f);
            Emit(Chips, position, 5, new Color(1f, 0.85f, 0.35f), 0.1f, 0.4f);
        }

        public void PlayMuzzle(Vector3 position) => PlayFlash(position, new Color(1f, 0.75f, 0.3f), 0.12f, 0.45f, 5f, 4f);

        public void PlayWreck(Vector3 position)
        {
            PlayFlash(position, new Color(1f, 0.55f, 0.1f), 0.6f, 3.5f, 15f, 8f);
            Emit(Chips, position, 10, new Color(0.18f, 0.16f, 0.15f), 0.28f, 1.3f);
            Emit(Chips, position, 6, new Color(1f, 0.6f, 0.15f), 0.18f, 0.7f);
            Emit(Smoke, position + Vector3.up * 0.4f, 7, new Color(0.25f, 0.24f, 0.23f, 0.7f), 1.4f, 2f);
        }

        /// <summary>Scrape against a barrier: a few short-lived sparks.</summary>
        public void PlaySparks(Vector3 position) => Emit(Chips, position, 3, new Color(1f, 0.8f, 0.3f), 0.08f, 0.3f);

        // ---------- particle bursts (two shared systems, fixed capacity, no allocation per burst) ----------
        [Tooltip("Upper bound on live chips/sparks and smoke puffs; bursts beyond it are dropped by the particle system.")]
        public int maxChips = 96, maxSmoke = 48;
        private ParticleSystem chips, smoke;
        private ParticleSystem.EmitParams emitParams;

        private ParticleSystem Chips => chips != null ? chips : (chips = BuildBurst("Chips", maxChips, 5f, 11f, 1f, false));
        private ParticleSystem Smoke => smoke != null ? smoke : (smoke = BuildBurst("Smoke", maxSmoke, 0.6f, 2f, -0.05f, true));

        private void Emit(ParticleSystem ps, Vector3 position, int count, Color color, float size, float lifetime)
        {
            if (ps == null) return;
            emitParams.position = position;
            emitParams.applyShapeToPosition = true;
            emitParams.startColor = color;
            emitParams.startSize = size;
            emitParams.startLifetime = lifetime;
            ps.Emit(emitParams, count);
        }

        private ParticleSystem BuildBurst(string name, int max, float minSpeed, float maxSpeed, float gravity, bool grow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(8, max);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.gravityModifier = gravity;
            main.playOnAwake = false;
            main.loop = true;    // keeps simulating; emission is off, so nothing spawns on its own
            var emission = ps.emission; emission.enabled = false;   // bursts come from Emit() only
            var shape = ps.shape; shape.shapeType = grow ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Sphere; shape.radius = grow ? 0.8f : 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(grow ? 0.8f : 1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sizeOverLife = ps.sizeOverLifetime; sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, grow ? AnimationCurve.Linear(0f, 0.6f, 1f, 2f) : AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            if (!grow) { var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f); }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var assets = RacingGame.World.WorldAssets.Instance;
            r.sharedMaterial = assets != null && assets.dustMaterial != null ? assets.dustMaterial : flashMaterial;
            ps.Play();
            return ps;
        }

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

        // The pool is created at runtime, so nothing in the scene references its material: it ships in
        // Resources (CarsGame/Flash shader). Shader.Find is only an Editor fallback — in a WebGL build an
        // unreferenced shader is stripped and new Material(null) would throw and kill every effect.
        private static Material BuildFallbackMaterial()
        {
            var shipped = Resources.Load<Material>("FX_Flash");
            if (shipped != null) return shipped;
            var shader = Shader.Find("CarsGame/Flash") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) { Debug.LogWarning("[EffectPool] no flash shader in the build; flashes disabled"); return null; }
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
