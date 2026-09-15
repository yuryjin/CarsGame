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

        public static CarController Find(CarController self, Vector3 origin, Vector3 forward, float coneDegrees, float minRange, float maxRange)
        {
            var director = RaceDirector.Current;
            if (director == null) return null;
            float cosCone = Mathf.Cos(coneDegrees * Mathf.Deg2Rad);
            int mask = ObstacleMask;
            CarController best = null; float bestDist = float.MaxValue;
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
                if (Vector3.Dot(to / dist, forward) < cosCone) continue;
                if (mask != 0 && Physics.Linecast(origin, aim, mask, QueryTriggerInteraction.Ignore)) continue;
                if (dist < bestDist) { bestDist = dist; best = other; }
            }
            return best;
        }
    }
}
