using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// A car without a human driver: something to ram and to shoot at. When the car has no
    /// <see cref="CarController"/> it sets up the same physics (mass, frictionless wheel spheres)
    /// as the player car so collisions feel alike; with a CarController (e.g. an AI opponent)
    /// that setup is left to the controller. Returns to its start position after being wrecked.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CarHealth))]
    public class TargetCar : MonoBehaviour
    {
        public float wheelRadius = 0.34f;
        [Tooltip("Rolling resistance so a shoved driverless car eventually stops.")]
        public float rollingDeceleration = 2f;

        private Rigidbody rb;
        private bool hasController;
        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            hasController = GetComponent<CarController>() != null;

            if (!hasController)
            {
                rb.mass = 1250f;
                rb.centerOfMass = new Vector3(0f, -0.4f, 0f);
                rb.linearDamping = 0.05f;
                rb.angularDamping = 2.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var wheels = new[]
                {
                    transform.Find("Wheel_FL"), transform.Find("Wheel_FR"),
                    transform.Find("Wheel_RL"), transform.Find("Wheel_RR")
                };
                CarController.CreateWheelColliders(transform, wheels, wheelRadius);
                CarController.ApplyFrictionlessBodyMaterial(gameObject);
            }

            startPosition = transform.position;
            startRotation = transform.rotation;

            var health = GetComponent<CarHealth>();
            health.showScreenBar = false;
            health.showWorldBar = true;
            if (!hasController) health.OnRespawned += ReturnToStart;
        }

        private void FixedUpdate()
        {
            if (hasController) return;

            Vector3 v = rb.linearVelocity;
            Vector3 horizontal = new Vector3(v.x, 0f, v.z);
            if (horizontal.sqrMagnitude < 0.01f) return;

            float deltaV = Mathf.Min(horizontal.magnitude, rollingDeceleration * Time.fixedDeltaTime);
            rb.AddForce(-horizontal.normalized * (deltaV * rb.mass), ForceMode.Impulse);
        }

        private void ReturnToStart()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(startPosition + Vector3.up * 0.3f, startRotation);
        }
    }
}
