using UnityEngine;
using RacingGame.World;

namespace RacingGame
{
    /// <summary>
    /// Grey smoke from the bonnet once a car drops below half HP, thicker as it nears zero, so damage
    /// is readable on rivals from a distance. One small capped particle system per car; the emission
    /// rate is refreshed on health changes only.
    /// </summary>
    [RequireComponent(typeof(CarHealth))]
    public sealed class CarDamageSmoke : MonoBehaviour
    {
        [Range(0f, 1f)] public float startFraction = 0.5f;
        public float maxRate = 22f;
        public int maxParticles = 40;

        private CarHealth health;
        private ParticleSystem ps;
        private ParticleSystem.EmissionModule emission;

        private void Awake()
        {
            health = GetComponent<CarHealth>();
            Build();
            health.OnHealthChanged += OnHealth;
            health.OnDied += OnDied;
            health.OnRespawned += OnRespawned;
            OnHealth(health.Health, health.maxHealth);
        }

        private void OnDestroy()
        {
            if (health == null) return;
            health.OnHealthChanged -= OnHealth;
            health.OnDied -= OnDied;
            health.OnRespawned -= OnRespawned;
        }

        public float Rate => emission.rateOverTime.constant;

        private void OnHealth(float current, float max)
        {
            float f = max > 0f ? current / max : 1f;
            float rate = health.IsDead || f >= startFraction ? 0f : Mathf.Lerp(maxRate, 4f, f / Mathf.Max(0.01f, startFraction));
            emission.rateOverTime = rate;
        }

        private void OnDied(GameObject _) { emission.rateOverTime = maxRate * 1.5f; }
        private void OnRespawned() { OnHealth(health.Health, health.maxHealth); }

        private void Build()
        {
            var go = new GameObject("DamageSmoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, 1.2f);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startColor = new Color(0.3f, 0.3f, 0.3f, 0.55f);
            main.gravityModifier = -0.08f;
            main.maxParticles = maxParticles;
            main.playOnAwake = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 15f; shape.radius = 0.3f;
            go.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.2f));
            emission = ps.emission; emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var assets = WorldAssets.Instance;
            if (assets != null && assets.dustMaterial != null) r.sharedMaterial = assets.dustMaterial;
            ps.Play();
        }
    }
}
