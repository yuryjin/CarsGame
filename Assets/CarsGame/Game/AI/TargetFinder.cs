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
                if (self != null && !Teams.Hostile(self.gameObject, other.gameObject)) continue;   // mission team-mates
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
                if (self != null && !Teams.Hostile(self.gameObject, other.gameObject)) continue;
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

        /// <summary>
        /// Mission units (drones, troopers, turrets, the boss) of the other side inside the cone, same rules as
        /// <see cref="Find"/>. Returns null at once outside a mission (no units registered).
        /// </summary>
        public static MissionUnit FindUnit(CarController self, Vector3 origin, Vector3 forward, float coneDegrees, float minRange, float maxRange, bool closestToAxis = false)
        {
            var units = MissionUnit.All;
            if (units.Count == 0) return null;
            float cosCone = Mathf.Cos(coneDegrees * Mathf.Deg2Rad);
            int mask = ObstacleMask;
            GameObject me = self != null ? self.gameObject : null;
            MissionUnit best = null; float bestScore = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || !u.Targetable || !Teams.Hostile(me, u.gameObject)) continue;
                Vector3 aim = u.AimPointFrom(origin);
                Vector3 to = aim - origin;
                float dist = to.magnitude;
                if (dist < minRange || dist > maxRange + u.Radius) continue;
                float dot = Vector3.Dot(to / Mathf.Max(0.01f, dist), forward);
                // A big target fills more of the cone: widen it by the unit's angular radius.
                float widened = Mathf.Cos(Mathf.Min(89f, coneDegrees + Mathf.Atan2(u.Radius, Mathf.Max(1f, dist)) * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                if (dot < Mathf.Min(cosCone, widened)) continue;
                // Weapon stations before the hull: the hull only wins when nothing else is in the cone.
                float score = (closestToAxis ? -dot : dist) + (u.Kind == UnitKind.BossHull ? 1000f : 0f);
                if (score >= bestScore) continue;
                if (mask != 0 && Physics.Linecast(origin, aim, mask, QueryTriggerInteraction.Ignore)) continue;
                bestScore = score; best = u;
            }
            return best;
        }

        /// <summary>
        /// Worth setting off this charge now: it is armed, a live rival is well inside the blast
        /// (<paramref name="reach"/> of its radius) and the owner is clear of it. Shared by the AI and touch auto-fire.
        /// </summary>
        public static bool ChargeWorthFiring(CarController owner, MineHazard charge, int tick, float reach = 0.8f)
        {
            if (charge == null || !charge.IsArmed(tick) || charge.Kind != MineKind.Charge) return false;
            var director = RaceDirector.Current;
            if (director == null) return false;
            Vector3 centre = charge.transform.position;
            float radius = charge.BlastRadius;
            if (owner != null && (owner.transform.position - centre).sqrMagnitude < (radius + 1f) * (radius + 1f)) return false;
            float hit = radius * reach;
            var cars = director.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                var other = cars[i];
                if (other == null || other == owner || !other.isActiveAndEnabled) continue;
                if (owner != null && !Teams.Hostile(owner.gameObject, other.gameObject)) continue;
                var h = other.GetComponent<CarHealth>();
                if (h != null && (h.IsDead || h.IsInvulnerable)) continue;
                if ((other.transform.position - centre).sqrMagnitude <= hit * hit) return true;
            }
            return false;
        }
    }
}
