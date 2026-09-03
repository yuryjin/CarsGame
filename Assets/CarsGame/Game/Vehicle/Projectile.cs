using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Pooled cannon shell. Lives on the "Projectile" layer (shell×shell collisions are off in the
    /// physics matrix), damages the <see cref="CarHealth"/> it hits, shoves the target and returns
    /// to the <see cref="RaceDirector"/> pool on first contact or when its tick lifetime ends.
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
        private readonly List<Collider> ignoredOwnerColliders = new List<Collider>(16);

        public GameObject Owner => owner;
        public bool IsLive => live;

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
            consumed = false;
            live = true;
            deathTick = spawn.Tick + SimulationDriver.SecondsToTicks(lifetime);

            if (spawn.Material != null) shellRenderer.sharedMaterial = spawn.Material;

            transform.position = spawn.Position;
            transform.rotation = Quaternion.LookRotation(spawn.Velocity.sqrMagnitude > 0.01f ? spawn.Velocity : Vector3.forward);
            gameObject.SetActive(true);

            // Never hit the car that fired us (the muzzle sits inside the owner's body box).
            // The pairs are remembered so they can be cleared when the shell is recycled.
            if (owner != null)
            {
                owner.GetComponentsInChildren(true, ignoredOwnerColliders);
                foreach (var c in ignoredOwnerColliders) if (c != null) Physics.IgnoreCollision(shellCollider, c, true);
            }

            rb.position = spawn.Position;
            rb.linearVelocity = spawn.Velocity;
            rb.angularVelocity = Vector3.zero;
        }

        public void SimulationStep(int tick)
        {
            if (live && tick >= deathTick) Deactivate();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (consumed || !live) return;
            consumed = true;

            var health = collision.collider.GetComponentInParent<CarHealth>();
            if (health != null && health.gameObject != owner)
            {
                bool applied = health.TakeDamage(damage, owner);
                if (applied && director != null) director.CountHit(owner, health.gameObject);
            }

            var targetBody = collision.rigidbody;
            if (targetBody != null && !targetBody.isKinematic)
            {
                Vector3 dir = rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity.normalized : transform.forward;
                targetBody.AddForceAtPosition(dir * hitImpulse, collision.GetContact(0).point, ForceMode.Impulse);
            }

            EffectPool.Instance.PlayShellImpact(collision.GetContact(0).point);
            Deactivate();
        }

        public void Deactivate()
        {
            if (!live) return;
            live = false;
            foreach (var c in ignoredOwnerColliders) if (c != null && shellCollider != null) Physics.IgnoreCollision(shellCollider, c, false);
            ignoredOwnerColliders.Clear();
            owner = null;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            gameObject.SetActive(false);
            if (director != null) director.ReturnToPool(this);
        }
    }
}
