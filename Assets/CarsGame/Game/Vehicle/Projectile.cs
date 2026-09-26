using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>
    /// Pooled projectile (cannon shell, machine gun bullet, freeze projectile).
    /// Lives on the "Projectile" layer, damages the <see cref="CarHealth"/> it hits,
    /// shoves the target and returns to the <see cref="RaceDirector"/> pool on first contact
    /// or when its tick lifetime ends.
    /// Every piece of per-shot state (owner, ignored collider pairs, velocity, consumed flag)
    /// is reset on <see cref="Activate"/>/<see cref="Deactivate"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public float damage = 25f;
        public float lifetime = 4f;
        public float hitImpulse = 4000f;

        private RaceDirector director;
        private GameObject owner;
        private Rigidbody rb;
        private Collider shellCollider;
        private Renderer shellRenderer;
        private bool consumed;
        private bool live;
        private int deathTick;
        private ProjectileKind kind;
        private StatusKind statusToApply;
        private float statusDuration;
        private Transform homingTarget;
        private int ricochets;
        private Vector3 travelVelocity;
        private Transform weaponVisual;
        private PrimaryWeaponType visualType;
        private bool visualInitialized;
        private MaterialPropertyBlock visualTint;
        public int RicochetsRemaining => ricochets;
        private readonly List<Collider> ignoredOwnerColliders = new List<Collider>(16);

        public GameObject Owner => owner;
        public bool IsLive => live;
        public ProjectileKind Kind => kind;
        public StatusKind StatusToApply => statusToApply;

        /// <summary>Builds one inactive shell for the pool.</summary>
        public static Projectile CreatePooled(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Cannon_Shell";
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * 0.22f;
            int layer = LayerMask.NameToLayer("Projectile");
            go.layer = layer >= 0 ? layer : 0;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var p = go.AddComponent<Projectile>();
            p.rb = rb;
            p.shellCollider = go.GetComponent<Collider>();
            p.shellRenderer = go.GetComponent<Renderer>();
            go.SetActive(false);
            return p;
        }

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (shellCollider == null) shellCollider = GetComponent<Collider>();
            if (shellRenderer == null) shellRenderer = GetComponent<Renderer>();
        }

        public void Activate(RaceDirector owningDirector, in ProjectileSpawn spawn)
        {
            director = owningDirector;
            owner = spawn.Owner;
            damage = spawn.Damage;
            kind = spawn.Kind;
            statusToApply = spawn.Status;
            statusDuration = spawn.StatusDuration;
            homingTarget = spawn.HomingTarget;
            ricochets = Mathf.Max(0, spawn.Ricochets);
            travelVelocity = spawn.Velocity;
            ConfigureVisual(spawn.Weapon);
            consumed = false;
            live = true;

            float effectiveLifetime = lifetime;
            float effectiveHitImpulse = 4000f;

            if (kind == ProjectileKind.Freeze)
            {
                effectiveLifetime = 2f;
                statusToApply = StatusKind.Frozen;
                if (statusDuration <= 0f) statusDuration = 1.5f;
                if (damage <= 0f) damage = 5f;
                effectiveHitImpulse = 1500f;
            }
            else if (kind == ProjectileKind.MachineGun)
            {
                effectiveLifetime = 0.4f;
                effectiveHitImpulse = 800f;
            }

            hitImpulse = spawn.Impact != 0f ? spawn.Impact : effectiveHitImpulse;
            deathTick = spawn.Tick + SimulationDriver.SecondsToTicks(effectiveLifetime);

            if (spawn.Material != null && shellRenderer != null)
            {
                shellRenderer.sharedMaterial = spawn.Material;
            }

            transform.position = spawn.Position;
            transform.rotation = Quaternion.LookRotation(spawn.Velocity.sqrMagnitude > 0.01f ? spawn.Velocity : Vector3.forward);
            gameObject.SetActive(true);

            // Never hit the car that fired us (the muzzle sits inside the owner's body box).
            // The pairs are remembered so they can be cleared when the shell is recycled.
            if (owner != null && shellCollider != null)
            {
                owner.GetComponentsInChildren(true, ignoredOwnerColliders);
                foreach (var c in ignoredOwnerColliders)
                {
                    if (c != null) Physics.IgnoreCollision(shellCollider, c, true);
                }
            }

            if (rb != null)
            {
                rb.useGravity = spawn.Weapon != PrimaryWeaponType.HomingRocket;
                rb.position = spawn.Position;
                rb.linearVelocity = spawn.Velocity;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public void SimulationStep(int tick)
        {
            if (live && rb != null && rb.linearVelocity.sqrMagnitude > 0.01f) travelVelocity = rb.linearVelocity;
            if (live && tick >= deathTick)
            {
                Deactivate();
                return;
            }

            if (live && homingTarget != null && rb != null)
            {
                Vector3 toTarget = homingTarget.position - transform.position;
                if (toTarget.sqrMagnitude > 0.01f && rb.linearVelocity.sqrMagnitude > 0.01f)
                {
                    float speed = rb.linearVelocity.magnitude;
                    float maxRadians = (kind == ProjectileKind.Freeze ? 30f : 75f) * Mathf.Deg2Rad * SimulationDriver.FixedStep;
                    Vector3 newVel = Vector3.RotateTowards(rb.linearVelocity, toTarget.normalized * speed, maxRadians, 0f);
                    rb.linearVelocity = newVel;
                    transform.rotation = Quaternion.LookRotation(newVel);
                }
            }
        }

        public bool TryRicochet(Vector3 normal)
        {
            if (!live || ricochets <= 0 || rb == null || normal.sqrMagnitude < 0.01f) return false;
            ricochets--;
            travelVelocity = Vector3.Reflect(travelVelocity, normal.normalized);
            rb.linearVelocity = travelVelocity;
            rb.position += normal.normalized * 0.15f;
            if (travelVelocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(travelVelocity);
            return true;
        }

        private void ConfigureVisual(PrimaryWeaponType weapon)
        {
            if (visualInitialized && visualType == weapon) return;
            visualInitialized = true;
            visualType = weapon;
            if (weaponVisual != null) { weaponVisual.gameObject.SetActive(false); WeaponArt.Destroy(weaponVisual.gameObject); }
            if (shellRenderer == null) return;
            bool custom = weapon == PrimaryWeaponType.GloveLauncher || weapon == PrimaryWeaponType.AnvilLauncher
                || weapon == PrimaryWeaponType.BowlingBall || weapon == PrimaryWeaponType.HomingRocket || weapon == PrimaryWeaponType.RicochetSaw;
            shellRenderer.enabled = !custom;
            transform.localScale = Vector3.one * (custom ? 0.65f : 0.22f);
            if (visualTint == null) visualTint = new MaterialPropertyBlock();
            visualTint.Clear();
            if ((int)weapon >= 10)
            {
                visualTint.SetColor("_BaseColor", WeaponArt.Tint(weapon));
                visualTint.SetColor("_Color", WeaponArt.Tint(weapon));
            }
            shellRenderer.SetPropertyBlock(visualTint);
            if (!custom) return;
            weaponVisual = new GameObject("Projectile_" + weapon).transform;
            weaponVisual.SetParent(transform, false);
            var color = WeaponArt.Material(WeaponArt.Tint(weapon));
            var metal = WeaponArt.Material(new Color(0.4f, 0.45f, 0.5f));
            if (weapon == PrimaryWeaponType.GloveLauncher)
            {
                WeaponArt.Part(weaponVisual, "Glove", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.2f, 0.9f, 1.3f), color);
                WeaponArt.Part(weaponVisual, "Cuff", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.65f), new Vector3(0.65f, 0.3f, 0.65f), metal).localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (weapon == PrimaryWeaponType.AnvilLauncher)
            {
                WeaponArt.Part(weaponVisual, "AnvilTop", PrimitiveType.Cube, Vector3.up * 0.3f, new Vector3(1.5f, 0.35f, 0.75f), metal);
                WeaponArt.Part(weaponVisual, "AnvilBase", PrimitiveType.Cube, Vector3.down * 0.3f, new Vector3(1.3f, 0.2f, 0.85f), metal);
                WeaponArt.Part(weaponVisual, "AnvilWaist", PrimitiveType.Cube, Vector3.zero, new Vector3(0.65f, 0.5f, 0.5f), metal);
            }
            else if (weapon == PrimaryWeaponType.BowlingBall)
            {
                WeaponArt.Part(weaponVisual, "BowlingBall", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.2f, color);
                for (int i = 0; i < 3; i++)
                    WeaponArt.Part(weaponVisual, "FingerHole", PrimitiveType.Sphere, new Vector3((i - 1) * 0.18f, 0.3f, 0.47f), Vector3.one * 0.13f, WeaponArt.Material(Color.black));
            }
            else if (weapon == PrimaryWeaponType.HomingRocket)
            {
                WeaponArt.Part(weaponVisual, "RocketBody", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.45f, 0.9f, 0.45f), metal).localRotation = Quaternion.Euler(90f, 0f, 0f);
                WeaponArt.Cone(weaponVisual, "RocketNose", 0.3f, 0.6f, color).SetLocalPositionAndRotation(new Vector3(0f, 0f, 0.65f), Quaternion.Euler(90f, 0f, 0f));
                WeaponArt.Part(weaponVisual, "RocketFlame", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.85f), new Vector3(0.3f, 0.3f, 0.9f), WeaponArt.Material(new Color(1f, 0.45f, 0.05f), true));
            }
            else
            {
                WeaponArt.Part(weaponVisual, "SawDisc", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.6f, 0.08f, 1.6f), metal);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI / 6f;
                    WeaponArt.Part(weaponVisual, "SawTooth", PrimitiveType.Cube, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.8f,
                        new Vector3(0.3f, 0.16f, 0.2f), color).localRotation = Quaternion.Euler(0f, -i * 30f, 0f);
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (consumed || !live) return;
            var health = collision.collider.GetComponentInParent<CarHealth>();
            // Mission units (drones, troopers, the boss) keep their own HP; a same-side shell just stops on them.
            var unit = health == null && MissionUnit.All.Count > 0 ? collision.collider.GetComponentInParent<MissionUnit>() : null;
            if (unit != null)
            {
                consumed = true;
                Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                if (unit.TakeHit(owner, damage, point) && director != null) director.CountHit(owner, unit.gameObject);
                if (EffectPool.Instance != null) EffectPool.Instance.PlayShellImpact(point);
                Deactivate();
                return;
            }
            if (health == null && collision.contactCount > 0 && TryRicochet(collision.GetContact(0).normal)) return;
            consumed = true;
            // Same side of a co-op mission: no friendly fire (untagged cars and owner-less shells stay hostile to all).
            bool isCarTarget = health != null && health.gameObject != owner && Teams.Hostile(owner, health.gameObject);
            HitResult hitResult = default;

            if (isCarTarget)
            {
                var hit = new HitInfo
                {
                    Attacker = owner,
                    Damage = damage,
                    Point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position,
                    Normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.up,
                    HitType = HitType.Projectile,
                    Status = statusToApply,
                    StatusDuration = statusDuration
                };
                hitResult = health.ApplyHit(in hit);
                if ((hitResult.DamageApplied || hitResult.StatusApplied) && !hitResult.ShieldAbsorbed && director != null)
                {
                    director.CountHit(owner, health.gameObject);
                }
            }

            var targetBody = collision.rigidbody;
            if (targetBody != null && !targetBody.isKinematic)
            {
                if (!isCarTarget || hitResult.DamageApplied)
                {
                    Vector3 dir = (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f) ? rb.linearVelocity.normalized : transform.forward;
                    targetBody.AddForceAtPosition(dir * hitImpulse, collision.GetContact(0).point, ForceMode.Impulse);
                }
            }

            if (EffectPool.Instance != null)
            {
                EffectPool.Instance.PlayShellImpact(collision.GetContact(0).point);
            }
            Deactivate();
        }

        public void Deactivate()
        {
            if (!live) return;
            live = false;
            foreach (var c in ignoredOwnerColliders)
            {
                if (c != null && shellCollider != null) Physics.IgnoreCollision(shellCollider, c, false);
            }
            ignoredOwnerColliders.Clear();
            owner = null;
            homingTarget = null;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            gameObject.SetActive(false);
            if (director != null) director.ReturnToPool(this);
        }
    }
}
