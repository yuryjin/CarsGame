using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Decides when an AI car fires. Every 10 ticks it looks for a rival inside a narrow cone
    /// ahead with clear line of sight, then holds the trigger for one tick; the cannon's
    /// magazine and cooldown do the rest. Never fires in the first seconds after GO, at
    /// wrecked or invulnerable cars, or while rubber-banding says the AI is far ahead.
    /// </summary>
    public sealed class AiGunner : MonoBehaviour
    {
        [Tooltip("Seconds between shots (set from the difficulty tier).")]
        public float fireCooldown = 1.5f;
        public float coneDegrees = 9f;
        public float minRange = 6f, maxRange = 45f;
        [Tooltip("No shooting for this long after the race starts.")]
        public float startGraceSeconds = 3f;
        [Tooltip("Set by the race manager while this car is far ahead of the player.")]
        public bool suppressed;

        public int ShotsRequested { get; private set; }

        private CarController car;
        private CarCannon cannon;
        private CarHealth health;
        private AiDriverProfile profile;
        private int nextShotTick;
        private int enabledTick = -1;
        private int obstacleMask;
        private RaceDirector director;

        private void Awake()
        {
            car = GetComponent<CarController>();
            cannon = GetComponent<CarCannon>();
            health = GetComponent<CarHealth>();
            int track = LayerMask.NameToLayer("Track"), ground = LayerMask.NameToLayer("Ground");
            obstacleMask = (track >= 0 ? 1 << track : 0) | (ground >= 0 ? 1 << ground : 0);
        }

        public void Initialize(AiDriverProfile driverProfile, float cooldown)
        {
            profile = driverProfile;
            fireCooldown = cooldown;
            enabledTick = -1;
        }

        /// <summary>Called by <see cref="AiDriver"/> every tick; returns the Fire level for the command.</summary>
        public bool WantsFire(int tick)
        {
            if (cannon == null || car == null) return false;
            if (enabledTick < 0) enabledTick = tick;
            if (tick - enabledTick < SimulationDriver.SecondsToTicks(startGraceSeconds)) return false;
            if (suppressed || tick < nextShotTick || !cannon.IsReady) return false;
            if (health != null && health.IsDead) return false;
            if (tick % 10 != 0) return false;

            if (director == null) director = RaceDirector.Instance;
            var target = FindTarget();
            if (target == null) return false;

            float aggression = profile != null ? profile.aggression : 0.5f;
            float scale = Mathf.Lerp(1.5f, 0.7f, aggression) * director.Rng.Range(RngStreams.Stream.Ai, 0.8f, 1.3f);
            nextShotTick = tick + SimulationDriver.SecondsToTicks(fireCooldown * scale);
            ShotsRequested++;
            return true;
        }

        private CarController FindTarget()
        {
            var muzzle = cannon.muzzle != null ? cannon.muzzle : transform;
            Vector3 origin = muzzle.position;
            Vector3 forward = muzzle.forward;
            float cosCone = Mathf.Cos(coneDegrees * Mathf.Deg2Rad);
            CarController best = null; float bestDist = float.MaxValue;
            var cars = director.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                var other = cars[i];
                if (other == null || other == car) continue;
                var h = other.GetComponent<CarHealth>();
                if (h != null && (h.IsDead || h.IsInvulnerable)) continue;
                Vector3 to = other.transform.position + Vector3.up * 0.5f - origin;
                float dist = to.magnitude;
                if (dist < minRange || dist > maxRange) continue;
                if (Vector3.Dot(to / dist, forward) < cosCone) continue;
                if (obstacleMask != 0 && Physics.Linecast(origin, other.transform.position + Vector3.up * 0.5f, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
                if (dist < bestDist) { bestDist = dist; best = other; }
            }
            return best;
        }
    }
}
