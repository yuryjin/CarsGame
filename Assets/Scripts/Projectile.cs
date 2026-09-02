using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Cannon shell fired by <see cref="CarCannon"/>. Damages the <see cref="CarHealth"/> it
    /// hits, gives the target a shove and destroys itself on the first contact.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public float damage = 25f;
        public float lifetime = 4f;
        public float hitImpulse = 4000f;

        private GameObject owner;
        private Rigidbody rb;
        private bool consumed;

        public static Projectile Spawn(GameObject owner, Vector3 position, Vector3 velocity, float damage, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Cannon_Shell";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.22f;
            go.layer = owner.layer;

            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = velocity;

            var projectile = go.AddComponent<Projectile>();
            projectile.owner = owner;
            projectile.damage = damage;

            // Never hit the car that fired us (the shell starts inside/next to its colliders).
            var shellCollider = go.GetComponent<Collider>();
            foreach (var ownerCollider in owner.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(shellCollider, ownerCollider, true);
            }

            return projectile;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            Destroy(gameObject, lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (consumed) return;
            consumed = true;

            var health = collision.collider.GetComponentInParent<CarHealth>();
            if (health != null && health.gameObject != owner)
            {
                health.TakeDamage(damage);
            }

            var targetBody = collision.rigidbody;
            if (targetBody != null && !targetBody.isKinematic)
            {
                Vector3 dir = rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity.normalized : transform.forward;
                targetBody.AddForceAtPosition(dir * hitImpulse, collision.GetContact(0).point, ForceMode.Impulse);
            }

            var flash = new GameObject("Shell_Impact");
            flash.transform.position = collision.GetContact(0).point;
            var fx = flash.AddComponent<ExplosionFlash>();
            fx.duration = 0.25f;
            fx.maxRadius = 0.9f;

            Destroy(gameObject);
        }
    }
}
