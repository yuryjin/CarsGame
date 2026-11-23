using System;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>
    /// Hit points of a car. Damage comes from hard collisions (walls, trees, other cars) and
    /// from projectiles (see <see cref="Projectile"/>). When HP reaches zero the car is wrecked
    /// for a few seconds and then respawns with full HP. Every timer (respawn, invulnerability,
    /// collision cooldown) counts simulation ticks, so pauses and frame hitches cannot desync it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarHealth : MonoBehaviour, ISimulated
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
        [Tooltip("Scale for hits on static scenery (barriers, trees, walls). A wall hit at racing speed used to take 60 HP; car-on-car ramming keeps full damage.")]
        public float staticDamageMultiplier = 0.5f;

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
        public event Action<CarHealth> OnWreck;
        public event Action<int, int> OnKillRegistered; // (killerId, victimId)
        public event Action OnRespawned;
        public event Action<HitInfo, HitResult> HitResolved;
        public int RepairsUsed { get; private set; }

        public float Health => currentHealth;
        public bool IsDead { get; private set; }
        public GameObject LastAttacker { get; private set; }
        public bool IsInvulnerable => NowTick < invulnerableUntilTick;
        public CarStatus Status => status;
        /// <summary>After the car controllers (1..n) and before the race director.</summary>
        public int SimulationOrder => 1000 + (controller != null ? controller.CarId : 0);

        private int currentTick = 0;
        private int invulnerableUntilTick = -1;
        private int lastCollisionDamageTick = -1000000;   // far past, no overflow in (now - last)
        private int respawnAtTick = int.MaxValue;
        private int NowTick => SimulationDriver.Instance != null ? Mathf.Max(currentTick, SimulationDriver.Instance.Tick) : currentTick;

        private Rigidbody rb;
        private CarController controller;
        private CarStatus status;
        private CarPaint paint;
        [Tooltip("Body blink on every hit that costs HP.")]
        public float hitFlashSeconds = 0.09f;
        public Color hitFlashColor = new Color(1f, 0.95f, 0.85f);
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            controller = GetComponent<CarController>();
            status = GetComponent<CarStatus>();
            paint = GetComponent<CarPaint>();
            currentHealth = maxHealth;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable() { SimulationDriver.Instance.Register(this); }
        private void OnDisable() { var d = SimulationDriver.Current; if (d != null) d.Unregister(this); }

        public void SimulationStep(int tick)
        {
            if (GameFlow.RoomReplica) return;
            currentTick = tick;
            if (IsDead && respawnOnDeath && tick >= respawnAtTick) Respawn();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsDead || IsInvulnerable) return;

            // Projectiles apply their own damage via TakeDamage - don't count the physical bump twice.
            if (collision.collider.GetComponentInParent<Projectile>() != null) return;

            // Ignore landings / driving over the road step: only mostly-horizontal impacts hurt.
            Vector3 avgNormal = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++) avgNormal += collision.GetContact(i).normal;
            // "Vertical" is along the car's own gravity, so touching down on a tunnel wall while wall riding is a landing too.
            Vector3 up = controller != null ? controller.GravityUp : Vector3.up;
            bool vertical = collision.contactCount > 0 && Vector3.Dot(avgNormal.normalized, up) > 0.7f;

            // impulse / mass = change of velocity caused by this impact
            float impactSpeed = collision.impulse.magnitude / rb.mass;
            int now = NowTick;
            bool onCooldown = now - lastCollisionDamageTick < SimulationDriver.SecondsToTicks(collisionDamageCooldown);
            bool isStatic = collision.rigidbody == null;
            float damage = vertical || onCooldown ? 0f
                : Mathf.Clamp((impactSpeed - minImpactSpeed) * damagePerImpactSpeed * (isStatic ? staticDamageMultiplier : 1f), 0f, maxCollisionDamage);

            if (logImpacts)
            {
                Debug.Log($"[CarHealth] {name} hit {collision.collider.name}: impact {impactSpeed:F1} m/s, " +
                          $"relVel {collision.relativeVelocity.magnitude:F1}, normal {avgNormal.normalized}, " +
                          $"vertical={vertical} cooldown={onCooldown} -> damage {damage:F1}", this);
            }

            if (damage <= 0f) return;

            lastCollisionDamageTick = now;
            // Ramming counts: the other car (if it is a car) gets the credit.
            GameObject attacker = collision.rigidbody != null && collision.rigidbody.GetComponent<CarHealth>() != null
                ? collision.rigidbody.gameObject : null;
            var hit = new HitInfo
            {
                Attacker = attacker,
                Damage = damage,
                Point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position,
                Normal = avgNormal.normalized,
                HitType = HitType.Collision
            };
            ApplyHit(in hit);
        }

        public HitResult ApplyHit(in HitInfo hit)
        {
            if (GameFlow.RoomReplica || IsDead || IsInvulnerable) return HitResult.Rejected;

            var result = new HitResult();
            float amount = hit.Damage * GlobalDamageMultiplier;

            if (amount > 0f)
            {
                if (status == null) status = GetComponent<CarStatus>();
                if (status != null && status.ConsumeShield())
                {
                    result.DamageApplied = true;
                    result.ShieldAbsorbed = true;
                    result.DamageDealt = 0f;
                    result.Fatal = false;
                    result.StatusApplied = false;
                    HitResolved?.Invoke(hit, result);
                    return result;
                }

                LastAttacker = hit.Attacker;   // credit goes to whoever dealt the fatal blow; walls (null) get no kill
                currentHealth = Mathf.Max(0f, currentHealth - amount);
                result.DamageApplied = true;
                result.DamageDealt = amount;

                if (currentHealth > 0f)
                {
                    if (paint == null) paint = GetComponent<CarPaint>();   // the spawner may add it after Awake
                    if (paint != null) paint.Flash(hitFlashSeconds, hitFlashColor);
                }
                OnHealthChanged?.Invoke(currentHealth, maxHealth);

                if (currentHealth <= 0f)
                {
                    result.Fatal = true;
                    Die(hit.Attacker);
                    HitResolved?.Invoke(hit, result);
                    return result;
                }
            }
            else
            {
                result.DamageApplied = true;
                result.DamageDealt = 0f;
            }

            if (hit.Status != StatusKind.None)
            {
                if (status == null) status = GetComponent<CarStatus>();
                if (status != null)
                {
                    float duration = hit.StatusDuration > 0f ? hit.StatusDuration
                        : (hit.StatusDurationTicks > 0 ? hit.StatusDurationTicks * SimulationDriver.FixedStep : 2.5f);
                    result.StatusApplied = status.ApplyStatus(hit.Status, duration);
                }
            }

            HitResolved?.Invoke(hit, result);
            return result;
        }

        public bool TakeDamage(float amount) => TakeDamage(amount, null);

        /// <summary>Event rule (e.g. Night Brawl ×1.5). Reset by the flow when a race ends.</summary>
        public static float GlobalDamageMultiplier = 1f;

        /// <summary>Returns true only if HP actually changed (dead/invulnerable cars reject damage, shield absorbs).</summary>
        public bool TakeDamage(float amount, GameObject attacker)
        {
            var hit = new HitInfo
            {
                Attacker = attacker,
                Damage = amount,
                HitType = HitType.Direct
            };
            var result = ApplyHit(in hit);
            return result.DamageApplied && !result.ShieldAbsorbed && result.DamageDealt > 0f;
        }

        public void ApplyRoomHealth(float value, bool dead)
        {
            if (!GameFlow.RoomReplica) return;
            if (IsDead != dead) { IsDead = dead; TintWreck(dead); }
            value = Mathf.Clamp(value,0f,maxHealth);
            if (Mathf.Approximately(currentHealth,value)) return;
            currentHealth = value;
            OnHealthChanged?.Invoke(value,maxHealth);
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
            if (IsDead || amount <= 0f || currentHealth >= maxHealth) return;
            RepairsUsed++;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>The carried bomb destroys its carrier even through a shield or reset invulnerability.</summary>
        public void Detonate(GameObject attacker)
        {
            if (IsDead) return;
            float damage = currentHealth;
            currentHealth = 0f;
            LastAttacker = attacker;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Die(attacker);
            HitResolved?.Invoke(new HitInfo(attacker, damage, HitType.Explosion),
                new HitResult { DamageApplied = true, DamageDealt = damage, Fatal = true });
        }

        private void Die(GameObject attacker)
        {
            IsDead = true;
            respawnAtTick = NowTick + SimulationDriver.SecondsToTicks(respawnDelay);

            SetControlsEnabled(false);
            if (paint == null) paint = GetComponent<CarPaint>();
            if (paint != null) paint.CancelFlash();
            TintWreck(true);
            SpawnExplosion();

            // A wreck sits like a brick.
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearDamping = 3f;
                rb.angularDamping = 3f;
            }

            OnDied?.Invoke(attacker);
            OnWreck?.Invoke(this);

            int victimId = controller != null ? controller.CarId : 0;
            int killerId = 0;
            if (attacker != null)
            {
                var killerCar = attacker.GetComponentInParent<CarController>();
                if (killerCar != null) killerId = killerCar.CarId;
            }
            OnKillRegistered?.Invoke(killerId, victimId);

            var director = RaceDirector.Current ?? FindAnyObjectByType<RaceDirector>() ?? RaceDirector.Instance;
            if (director != null)
            {
                director.RegisterKill(killerId, victimId);
                director.SpawnOilSlick(transform.position);
            }
        }

        /// <summary>Rewarded "continue" in a one-life race: back on the track now with full HP and the respawn shield.</summary>
        public void ReviveNow()
        {
            if (!IsDead) return;
            Respawn();
        }

        private void Respawn()
        {
            var arena = GetComponent<RaceParticipant>()?.Arena;
            Vector3 entrance = default;
            Quaternion heading = Quaternion.identity;
            if (arena != null && !arena.TryGetRecoveryPose(controller, out entrance, out heading))
            {
                respawnAtTick = NowTick + SimulationDriver.SecondsToTicks(.25f);
                return;
            }
            IsDead = false;
            respawnAtTick = int.MaxValue;
            LastAttacker = null;
            currentHealth = maxHealth;

            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearDamping = 0.05f;
                rb.angularDamping = 2.5f;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (arena != null)
            {
                controller.Teleport(entrance, heading);
                rb.linearVelocity = heading * Vector3.forward * 12.5f;
            }
            else if (controller != null) controller.ResetCar();
            if (status != null) status.ClearAllStatuses();

            TintWreck(false);
            SetControlsEnabled(true);
            SetInvulnerable(respawnInvulnerability);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnRespawned?.Invoke();
        }

        public void SetInvulnerable(float seconds)
        {
            invulnerableUntilTick = Mathf.Max(invulnerableUntilTick, NowTick + SimulationDriver.SecondsToTicks(seconds));
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (controller != null) controller.enabled = enabled;

            var cannon = GetComponent<CarCannon>();
            if (cannon != null) cannon.enabled = enabled;

            var weapons = GetComponent<CarWeapons>();
            if (weapons != null) weapons.enabled = enabled;
        }

        internal void RefreshBodyRenderers()
        {
            renderers = null;
            if (IsDead) TintWreck(true);
        }

        private void TintWreck(bool wrecked)
        {
            if (renderers == null)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
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
            var pool = EffectPool.Current;
            if (pool != null)
            {
                pool.PlayWreck(transform.position + Vector3.up * 0.6f);
            }
            else if (Application.isPlaying)
            {
                EffectPool.Instance.PlayWreck(transform.position + Vector3.up * 0.6f);
            }
        }
    }
}
