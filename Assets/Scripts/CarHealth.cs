using System;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Hit points of a car. Damage comes from hard collisions (walls, trees, other cars) and
    /// from projectiles (see <see cref="Projectile"/>). When HP reaches zero the car is wrecked
    /// for a few seconds and then respawns with full HP.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarHealth : MonoBehaviour
    {
        [Header("Health")]
        public float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        [Header("Collision Damage")]
        [Tooltip("Velocity change (m/s) below which a bump does no damage - kerbs, light touches.")]
        public float minImpactSpeed = 3f;
        [Tooltip("HP lost per 1 m/s of velocity change above the threshold.")]
        public float damagePerImpactSpeed = 4f;
        [Tooltip("Maximum damage from a single collision.")]
        public float maxCollisionDamage = 60f;
        [Tooltip("Minimum time between two collision damage events.")]
        public float collisionDamageCooldown = 0.25f;

        [Header("Death")]
        public float respawnDelay = 3f;
        public bool respawnOnDeath = true;

        [Header("HUD")]
        public bool showScreenBar = true;      // big bar bottom-right (player)
        public bool showWorldBar = false;      // small bar above the car (targets)

        [Header("Debug")]
        [Tooltip("Log every collision with its impact speed and the damage it caused.")]
        public bool logImpacts = false;

        public event Action<float, float> OnHealthChanged; // current, max
        public event Action OnDied;
        public event Action OnRespawned;

        public float Health => currentHealth;
        public bool IsDead { get; private set; }

        private Rigidbody rb;
        private float lastCollisionDamageTime = -10f;
        private float respawnTimer;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            currentHealth = maxHealth;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!IsDead || !respawnOnDeath) return;

            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) Respawn();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsDead) return;

            // Projectiles apply their own damage via TakeDamage - don't count the physical bump twice.
            if (collision.collider.GetComponentInParent<Projectile>() != null) return;

            // Ignore landings / driving over the road step: only mostly-horizontal impacts hurt.
            Vector3 avgNormal = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++) avgNormal += collision.GetContact(i).normal;
            bool vertical = collision.contactCount > 0 && Vector3.Dot(avgNormal.normalized, Vector3.up) > 0.7f;

            // impulse / mass = change of velocity caused by this impact
            float impactSpeed = collision.impulse.magnitude / rb.mass;
            bool onCooldown = Time.time - lastCollisionDamageTime < collisionDamageCooldown;
            float damage = vertical || onCooldown ? 0f
                : Mathf.Clamp((impactSpeed - minImpactSpeed) * damagePerImpactSpeed, 0f, maxCollisionDamage);

            if (logImpacts)
            {
                Debug.Log($"[CarHealth] {name} hit {collision.collider.name}: impact {impactSpeed:F1} m/s, " +
                          $"relVel {collision.relativeVelocity.magnitude:F1}, normal {avgNormal.normalized}, " +
                          $"vertical={vertical} cooldown={onCooldown} -> damage {damage:F1}", this);
            }

            if (damage <= 0f) return;

            lastCollisionDamageTime = Time.time;
            TakeDamage(damage);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die()
        {
            IsDead = true;
            respawnTimer = respawnDelay;

            SetControlsEnabled(false);
            TintWreck(true);
            SpawnExplosion();

            // A wreck sits like a brick.
            rb.linearDamping = 3f;
            rb.angularDamping = 3f;

            OnDied?.Invoke();
        }

        private void Respawn()
        {
            IsDead = false;
            currentHealth = maxHealth;

            rb.linearDamping = 0.05f;
            rb.angularDamping = 2.5f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            var controller = GetComponent<CarController>();
            if (controller != null) controller.ResetCar();

            TintWreck(false);
            SetControlsEnabled(true);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnRespawned?.Invoke();
        }

        private void SetControlsEnabled(bool enabled)
        {
            var controller = GetComponent<CarController>();
            if (controller != null) controller.enabled = enabled;

            var cannon = GetComponent<CarCannon>();
            if (cannon != null) cannon.enabled = enabled;
        }

        private void TintWreck(bool wrecked)
        {
            if (renderers == null)
            {
                renderers = GetComponentsInChildren<Renderer>();
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sharedMaterial == null) continue;

                if (wrecked)
                {
                    propertyBlock.Clear();
                    string prop = r.sharedMaterial.HasProperty(BaseColorId) ? "_BaseColor" : "_Color";
                    propertyBlock.SetColor(prop, new Color(0.12f, 0.1f, 0.08f));
                    r.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }

        private void SpawnExplosion()
        {
            var fx = new GameObject("Explosion_FX");
            fx.transform.position = transform.position + Vector3.up * 0.6f;
            fx.AddComponent<ExplosionFlash>();
        }

        private void OnGUI()
        {
            if (showScreenBar) DrawScreenBar();
            if (showWorldBar) DrawWorldBar();
        }

        private void DrawScreenBar()
        {
            const float width = 260f, height = 26f;
            var rect = new Rect(Screen.width - width - 20f, Screen.height - height - 20f, width, height);
            DrawBar(rect, true);
        }

        private void DrawWorldBar()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
            if (screen.z < 0f) return;

            const float width = 90f, height = 10f;
            var rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - height, width, height);
            DrawBar(rect, false);
        }

        private void DrawBar(Rect rect, bool withText)
        {
            float t = maxHealth > 0f ? currentHealth / maxHealth : 0f;

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.2f, 0.85f, 0.25f), t);
            var inner = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * t, rect.height - 4f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            GUI.color = prev;

            if (!withText) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.white;
            string text = IsDead
                ? $"РАЗБИТА  ({Mathf.CeilToInt(respawnTimer)}с)"
                : $"HP {Mathf.CeilToInt(currentHealth)} / {Mathf.RoundToInt(maxHealth)}";
            GUI.Label(rect, text, style);
        }
    }

    /// <summary>Cheap explosion: an expanding orange sphere plus a point light, gone after half a second.</summary>
    public class ExplosionFlash : MonoBehaviour
    {
        public float duration = 0.6f;
        public float maxRadius = 3.5f;

        private float time;
        private Transform sphere;
        private Light flashLight;
        private Material material;

        private void Start()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            sphere = go.transform;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { color = new Color(1f, 0.55f, 0.1f) };
            go.GetComponent<Renderer>().material = material;

            flashLight = gameObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1f, 0.6f, 0.2f);
            flashLight.range = 15f;
            flashLight.intensity = 8f;
        }

        private void Update()
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            if (sphere != null) sphere.localScale = Vector3.one * Mathf.Lerp(0.3f, maxRadius, Mathf.Sqrt(t));
            if (flashLight != null) flashLight.intensity = Mathf.Lerp(8f, 0f, t);
            if (material != null)
            {
                var c = material.color;
                c.a = 1f - t;
                material.color = c;
            }

            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
