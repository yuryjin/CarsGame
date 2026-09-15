using UnityEngine;
using RacingGame.World;

namespace RacingGame
{
    /// <summary>
    /// Player-only feedback: dust when driving on grass, camera impulse on damage and hard landings,
    /// headlights on night presets. Added to the player car by GameFlow after the field spawns.
    /// </summary>
    public sealed class PlayerEffects : MonoBehaviour
    {
        public float dustMinSpeedKmh = 15f;
        public float dustRatePerKmh = 0.9f;

        private CarController car;
        private CarHealth health;
        private ParticleSystem dust;
        private ParticleSystem.EmissionModule emission;
        private CameraImpulse impulse;
        private float airTime;
        private bool wasGrounded = true;
        private Light headlight;

        private void Awake()
        {
            car = GetComponent<CarController>();
            health = GetComponent<CarHealth>();
            impulse = Camera.main != null ? Camera.main.GetComponent<CameraImpulse>() : null;
            if (health != null) health.OnHealthChanged += OnHealth;
            BuildDust();
            var sky = SkyDirector.Instance;
            if (sky != null) { ApplySky(sky.Current); sky.Applied += ApplySky; }
        }

        private void OnDestroy()
        {
            if (health != null) health.OnHealthChanged -= OnHealth;
            var sky = SkyDirector.Instance;
            if (sky != null) sky.Applied -= ApplySky;
        }

        private float lastHealth = -1f;
        private void OnHealth(float current, float max)
        {
            if (lastHealth >= 0f && current < lastHealth && impulse != null) impulse.AddImpulse(Mathf.Clamp01((lastHealth - current) / 40f) * 0.45f + 0.08f);
            lastHealth = current;
        }

        private void ApplySky(SkyPreset p)
        {
            bool on = p != null && p.headlights;
            if (on && headlight == null)
            {
                var go = new GameObject("Headlight");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.7f, 1.6f);
                go.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
                headlight = go.AddComponent<Light>();
                headlight.type = LightType.Spot; headlight.spotAngle = 70f; headlight.innerSpotAngle = 40f; headlight.range = 45f;
                headlight.intensity = 4f; headlight.color = new Color(1f, 0.93f, 0.8f); headlight.shadows = LightShadows.None;
            }
            if (headlight != null) headlight.enabled = on;
        }

        private void BuildDust()
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, -1.4f);
            go.transform.localRotation = Quaternion.Euler(-100f, 0f, 0f);   // backwards and slightly up
            dust = go.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
            main.startColor = new Color(0.66f, 0.58f, 0.42f, 0.45f);
            main.gravityModifier = -0.02f;
            main.maxParticles = 120;
            main.playOnAwake = false;
            var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.6f;
            var col = dust.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = dust.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            emission = dust.emission; emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var assets = WorldAssets.Instance;
            if (assets != null && assets.dustMaterial != null) r.sharedMaterial = assets.dustMaterial;
            dust.Play();
        }

        // Scraping a barrier or prop: sparks at the contact, a light shake. Throttled; mostly-vertical
        // contacts (the road itself, kerbs under the wheels) are ignored.
        [Tooltip("Relative speed (m/s) above which a side contact throws sparks.")]
        public float scrapeMinSpeed = 3f;
        private int nextSparkFrame;
        public int SparkBursts { get; private set; }

        private void OnCollisionStay(Collision collision)
        {
            if (Time.frameCount < nextSparkFrame || collision.contactCount == 0) return;
            if (collision.rigidbody != null) return;                      // cars bump, barriers scrape
            var contact = collision.GetContact(0);
            if (Mathf.Abs(Vector3.Dot(contact.normal, Vector3.up)) > 0.6f) return;
            if (collision.relativeVelocity.magnitude < scrapeMinSpeed) return;
            nextSparkFrame = Time.frameCount + 6;
            SparkBursts++;
            EffectPool.Instance.PlaySparks(contact.point);
            if (impulse != null) impulse.AddImpulse(0.04f);
        }

        private void Update()
        {
            if (car == null) return;
            float speed = Mathf.Abs(car.SpeedKmh);
            bool onGrass = car.Surface == GroundSurface.Ground && speed > dustMinSpeedKmh;
            emission.rateOverTime = onGrass ? Mathf.Min(90f, speed * dustRatePerKmh) : 0f;

            // Landing impulse after a real jump.
            if (!car.IsGrounded) airTime += Time.deltaTime;
            else
            {
                if (!wasGrounded && airTime > 0.35f && impulse != null) impulse.AddImpulse(Mathf.Clamp01(airTime * 0.5f) * 0.3f);
                airTime = 0f;
            }
            wasGrounded = car.IsGrounded;
        }
    }
}
