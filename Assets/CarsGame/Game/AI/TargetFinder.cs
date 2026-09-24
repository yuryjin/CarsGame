using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// The one rule for "who is in my sights": nearest live, vulnerable rival inside a cone in front of
    /// the muzzle, within range, with no track or ground geometry in between. Shared by the AI gunner
    /// and the player's reticle so both see the same targets. No allocations.
    /// </summary>
    public static class TargetFinder
    {
        private static int obstacleMask = -1;

        /// <summary>Track and Ground layers: what blocks a shot.</summary>
        public static int ObstacleMask
        {
            get
            {
                if (obstacleMask < 0)
                {
                    int track = LayerMask.NameToLayer("Track"), ground = LayerMask.NameToLayer("Ground");
                    obstacleMask = (track >= 0 ? 1 << track : 0) | (ground >= 0 ? 1 << ground : 0);
                }
                return obstacleMask;
            }
        }

        /// <param name="leadShellSpeed">When &gt; 0, test the cone against where the target will be when a shell of this speed arrives.</param>
        /// <param name="closestToAxis">Rank by angle off <paramref name="forward"/> instead of distance (a turret keeps the car dead ahead).</param>
        public static CarController Find(CarController self, Vector3 origin, Vector3 forward, float coneDegrees, float minRange, float maxRange, float leadShellSpeed = 0f, bool closestToAxis = false)
        {
            var director = RaceDirector.Current ?? Object.FindAnyObjectByType<RaceDirector>();
            if (director == null) return null;
            float cosCone = Mathf.Cos(coneDegrees * Mathf.Deg2Rad);
            int mask = ObstacleMask;
            CarController best = null; float bestScore = float.MaxValue;
            var cars = director.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                var other = cars[i];
                if (other == null || other == self || !other.isActiveAndEnabled) continue;
                var h = other.GetComponent<CarHealth>();
                if (h != null && (h.IsDead || h.IsInvulnerable)) continue;
                Vector3 aim = other.transform.position + Vector3.up * 0.5f;
                Vector3 to = aim - origin;
                float dist = to.magnitude;
                if (dist < minRange || dist > maxRange) continue;
                float dot;
                if (leadShellSpeed > 0f && other.Body != null)
                {
                    Vector3 lead = aim + other.Body.linearVelocity * (dist / leadShellSpeed);
                    Vector3 toLead = lead - origin;
                    float leadDist = toLead.magnitude;
                    if (leadDist < 0.01f) continue;
                    dot = Vector3.Dot(toLead / leadDist, forward);
                }
                else dot = Vector3.Dot(to / dist, forward);
                if (dot < cosCone) continue;
                float score = closestToAxis ? -dot : dist;
                if (score >= bestScore) continue;
                if (mask != 0 && Physics.Linecast(origin, aim, mask, QueryTriggerInteraction.Ignore)) continue;
                bestScore = score; best = other;
            }
            return best;
        }

        /// <summary>
        /// Finds the closest trailing rival behind the car within a distance range and lateral window.
        /// Used for hybrid auto-dropping mines and AI defensive secondary deployment.
        /// </summary>
        public static CarController FindPursuer(CarController self, Vector3 origin, Vector3 rearDirection, float minDistance = 15f, float maxDistance = 25f, float maxLateralOffset = 3f)
        {
            var director = RaceDirector.Current ?? Object.FindAnyObjectByType<RaceDirector>();
            if (director == null) return null;
            var cars = director.Cars;
            CarController best = null;
            float bestDist = float.MaxValue;
            int mask = ObstacleMask;
            for (int i = 0; i < cars.Count; i++)
            {
                var other = cars[i];
                if (other == null || other == self || !other.isActiveAndEnabled) continue;
                var h = other.GetComponent<CarHealth>();
                if (h != null && (h.IsDead || h.IsInvulnerable)) continue;
                Vector3 aim = other.transform.position + Vector3.up * 0.5f;
                Vector3 toOther = aim - origin;
                float distRear = Vector3.Dot(toOther, rearDirection);
                if (distRear < minDistance || distRear > maxDistance) continue;
                Vector3 lateral = toOther - rearDirection * distRear;
                lateral.y = 0f;
                if (lateral.magnitude > maxLateralOffset) continue;
                if (mask != 0 && Physics.Linecast(origin, aim, mask, QueryTriggerInteraction.Ignore)) continue;
                if (distRear < bestDist)
                {
                    bestDist = distRear;
                    best = other;
                }
            }
            return best;
        }
    }
}
