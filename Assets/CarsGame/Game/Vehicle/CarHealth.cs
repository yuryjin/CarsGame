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
        [Tooltip("Seconds of immunity after a respawn so a car is not wrecked twice in a row.")]
        public float respawnInvulnerability = 2f;

        [Header("HUD (read by the UI layer)")]
        public bool showScreenBar = true;
        public bool showWorldBar = false;

        [Header("Debug")]
        [Tooltip("Log every collision with its impact speed and the damage it caused.")]
        public bool logImpacts = false;

        public event Action<float, float> OnHealthChanged; // current, max
        public event Action<GameObject> OnDied; // attacker (may be null)
        public event Action OnRespawned;

        public float Health => currentHealth;
        public bool IsDead { get; private set; }
        public GameObject LastAttacker { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        private float invulnerableUntil = -1f;

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
            if (IsDead || IsInvulnerable) return;

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
            // Ramming counts: the other car (if it is a car) gets the credit.
            GameObject attacker = collision.rigidbody != null && collision.rigidbody.GetComponent<CarHealth>() != null
                ? collision.rigidbody.gameObject : null;
            TakeDamage(damage, attacker);
        }

        public bool TakeDamage(float amount) => TakeDamage(amount, null);

        /// <summary>Returns true only if HP actually changed (dead/invulnerable cars reject damage).</summary>
        /// <summary>Event rule (e.g. Night Brawl ×1.5). Reset by the flow when a race ends.</summary>
        public static float GlobalDamageMultiplier = 1f;

        public bool TakeDamage(float amount, GameObject attacker)
        {
            amount *= GlobalDamageMultiplier;
            if (IsDead || IsInvulnerable || amount <= 0f) return false;

            LastAttacker = attacker;   // credit goes to whoever dealt the fatal blow; walls (null) get no kill
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f) Die(attacker);
            return true;
        }

        /// <summary>Change the pool (difficulty tiers, armour upgrades); current HP follows and the event fires.</summary>
        public void SetMaxHealth(float value, bool refill)
        {
            maxHealth = Mathf.Max(1f, value);
            currentHealth = refill ? maxHealth : Mathf.Min(currentHealth, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die(GameObject attacker)
        {
            IsDead = true;
            respawnTimer = respawnDelay;

            SetControlsEnabled(false);
            TintWreck(true);
            SpawnExplosion();

            // A wreck sits like a brick.
            rb.linearDamping = 3f;
            rb.angularDamping = 3f;

            OnDied?.Invoke(attacker);
        }

        private void Respawn()
        {
            IsDead = false;
            LastAttacker = null;
            currentHealth = maxHealth;

            rb.linearDamping = 0.05f;
            rb.angularDamping = 2.5f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            var controller = GetComponent<CarController>();
            if (controller != null) controller.ResetCar();

            TintWreck(false);
            SetControlsEnabled(true);
            SetInvulnerable(respawnInvulnerability);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnRespawned?.Invoke();
        }

        public void SetInvulnerable(float seconds)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
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

            // Clearing the blocks wiped the body colour; the paint component owns it.
            if (!wrecked) { var paint = GetComponent<CarPaint>(); if (paint != null) paint.Apply(); }
        }

        private void SpawnExplosion()
        {
            EffectPool.Instance.PlayWreck(transform.position + Vector3.up * 0.6f);
        }
    }
}
