using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>Reusable boost aperture. Compound car colliders count as one passage.</summary>
    public sealed class BoostGate : MonoBehaviour
    {
        public const float Radius = 6f;
        public const float BoostSeconds = 2.5f;
        private readonly Dictionary<int, int> nextBoostTick = new Dictionary<int, int>();

        private void OnTriggerEnter(Collider other)
        {
            var car = other.GetComponentInParent<CarController>();
            if (car == null || !car.inputEnabled || car.Body.isKinematic) return;
            var health = car.GetComponent<CarHealth>();
            if (health != null && health.IsDead) return;
            Vector3 local = transform.InverseTransformPoint(car.Body.worldCenterOfMass);
            if (local.x * local.x + local.y * local.y > Radius * Radius) return;
            float speed = Vector3.Dot(car.Body.linearVelocity, transform.forward);
            if (speed <= 1f) return; // Only a forward passage activates the gate.
            int tick = SimulationDriver.Instance.Tick;
            if (nextBoostTick.TryGetValue(car.CarId, out int next) && tick < next) return;
            nextBoostTick[car.CarId] = tick + SimulationDriver.SecondsToTicks(BoostSeconds);
            car.ApplyBoost(BoostSeconds);
            float kick = Mathf.Clamp(car.maxSpeed / 3.6f - speed, 0f, 8f);
            car.Body.linearVelocity += transform.forward * kick;
        }
    }
}
