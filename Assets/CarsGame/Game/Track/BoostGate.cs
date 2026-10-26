using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>Reusable boost aperture. Compound car colliders count as one passage.</summary>
    public sealed class BoostGate : MonoBehaviour
    {
        public const float Radius = 6f;
        public const float BoostSeconds = 2.5f;
        [Tooltip("Round aperture around the local Z axis the car's centre must pass through.")]
        public float radius = Radius;
        [Tooltip("Positive for a flat boost strip: useful half-width, independent of centre-of-mass height. Zero uses the round aperture.")]
        public float stripHalfWidth;
        public bool ceiling;
        private readonly Dictionary<int, int> nextBoostTick = new Dictionary<int, int>();
        private sealed class Passage
        {
            public readonly HashSet<Collider> Colliders = new HashSet<Collider>();
            public bool Awarded;
        }
        private readonly Dictionary<int, Passage> passages = new Dictionary<int, Passage>();

        private void OnTriggerEnter(Collider other) => TryBoost(other);
        private void OnTriggerStay(Collider other) => TryBoost(other);

        private void OnTriggerExit(Collider other)
        {
            var car = other.GetComponentInParent<CarController>();
            if (car == null || !passages.TryGetValue(car.CarId, out var passage)) return;
            passage.Colliders.Remove(other);
            if (passage.Colliders.Count == 0) passages.Remove(car.CarId);
        }

        private void OnDisable() => passages.Clear();

        private void TryBoost(Collider other)
        {
            var car = other.GetComponentInParent<CarController>();
            if (car == null) return;
            if (!passages.TryGetValue(car.CarId, out var passage))
            {
                passage = new Passage();
                passages.Add(car.CarId, passage);
            }
            passage.Colliders.Add(other);
            // A car can enter off-centre and align while still inside the trigger.
            // Award only once until every collider has left, including after the cooldown.
            if (passage.Awarded || !car.inputEnabled || car.Body.isKinematic) return;
            var health = car.GetComponent<CarHealth>();
            if (health != null && health.IsDead) return;
            Vector3 local = transform.InverseTransformPoint(car.Body.worldCenterOfMass);
            if (stripHalfWidth > 0f ? Mathf.Abs(local.x) > stripHalfWidth
                : local.x * local.x + local.y * local.y > radius * radius) return;
            float speed = Vector3.Dot(car.Body.linearVelocity, transform.forward);
            if (speed <= 1f) return; // Only a forward passage activates the gate.
            int tick = SimulationDriver.Instance.Tick;
            if (nextBoostTick.TryGetValue(car.CarId, out int next) && tick < next) return;
            passage.Awarded = true;
            nextBoostTick[car.CarId] = tick + SimulationDriver.SecondsToTicks(BoostSeconds);
            car.ApplyBoost(BoostSeconds);
            if (ceiling)
            {
                var participant = car.GetComponent<RaceParticipant>();
                if (participant != null) participant.CeilingBoosts++;
            }
            float kick = Mathf.Clamp(car.maxSpeed / 3.6f - speed, 0f, 8f);
            car.Body.linearVelocity += transform.forward * kick;
        }
    }
}
