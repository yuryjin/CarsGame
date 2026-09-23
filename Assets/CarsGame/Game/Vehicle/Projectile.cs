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

            hitImpulse = effectiveHitImpulse;
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
                rb.position = spawn.Position;
                rb.linearVelocity = spawn.Velocity;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public void SimulationStep(int tick)
        {
            if (live && tick >= deathTick)
            {
                Deactivate();
                return;
            }

            if (live && homingTarget != null && kind == ProjectileKind.Freeze && rb != null)
            {
                Vector3 toTarget = homingTarget.position - transform.position;
                if (toTarget.sqrMagnitude > 0.01f && rb.linearVelocity.sqrMagnitude > 0.01f)
                {
                    float speed = rb.linearVelocity.magnitude;
                    float maxRadians = 30f * Mathf.Deg2Rad * SimulationDriver.FixedStep;
                    Vector3 newVel = Vector3.RotateTowards(rb.linearVelocity, toTarget.normalized * speed, maxRadians, 0f);
                    rb.linearVelocity = newVel;
                    transform.rotation = Quaternion.LookRotation(newVel);
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (consumed || !live) return;
            consumed = true;

            var health = collision.collider.GetComponentInParent<CarHealth>();
            bool isCarTarget = health != null && health.gameObject != owner;
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
