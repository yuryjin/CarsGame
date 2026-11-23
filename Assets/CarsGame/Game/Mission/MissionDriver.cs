using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Bot driver of the boss mission, for both sides. Allies circle the fortress at speed (never in front of the
    /// bucket wheel), peel off for escorts and troopers nearby, and let the gunner pick targets. Escorts stay near the
    /// fortress and run down any car of the player's side that comes close. Steering is a plain seek with whisker
    /// rays for rocks and hulls, reversing out when stuck and a reset when flipped.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public sealed class MissionDriver : MonoBehaviour, ICarInputSource
    {
        public enum Side { Ally, Escort }

        private MissionDirector mission;
        private AiDriverProfile profile;
        private CarController car;
        private AiGunner gunner;
        private Side side;
        private int slot;
        private Vector3 goal;
        private float goalSpeedKmh = 90f;
        private int nextPlanTick, reverseUntilTick, stuckTicks, flippedTicks, lastReverseTick = -100000;
        private float orbitSign = 1f;
        private int obstacleMask;

        private void Awake()
        {
            car = GetComponent<CarController>();
            car.playerControlled = false;
            int track = LayerMask.NameToLayer("Track");
            obstacleMask = (track >= 0 ? 1 << track : 0) | 1;   // rocks, hulks and the terrain (Track) + the fortress and outposts (Default)
        }

        public void Initialize(MissionDirector director, AiDriverProfile driverProfile, Side team, int index)
        {
            mission = director;
            profile = driverProfile != null ? driverProfile : AiDriverProfile.Default;
            side = team;
            slot = index;
            orbitSign = index % 2 == 0 ? 1f : -1f;
            car = GetComponent<CarController>();
            gunner = GetComponent<AiGunner>();
        }

        public DriveCommand Sample(int tick)
        {
            if (mission == null || car == null || !car.isActiveAndEnabled || !car.inputEnabled || !MissionUnit.Running) return DriveCommand.None(tick);
            if (gunner == null) gunner = GetComponent<AiGunner>();
            if (tick >= nextPlanTick) { Plan(); nextPlanTick = tick + 12 + slot; }

            bool flipped = Vector3.Dot(transform.up, Vector3.up) < 0.25f;
            flippedTicks = flipped ? flippedTicks + 1 : 0;
            if (flippedTicks > SimulationDriver.SecondsToTicks(1.5f)) { flippedTicks = 0; return new DriveCommand { Tick = tick, ResetEdge = true }; }

            Vector3 delta = goal - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;
            Vector3 local = transform.InverseTransformDirection(dist > 0.01f ? delta / dist : transform.forward);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float speed = Mathf.Abs(car.SpeedKmh);

            if (tick < reverseUntilTick)
                return new DriveCommand { Tick = tick, Throttle = -1f, Steer = angle > 0f ? -1f : 1f, Fire = gunner != null && gunner.WantsFire(tick) };

            float steer = Mathf.Clamp(angle / 32f - car.Body.angularVelocity.y * 0.2f + Whiskers(speed), -1f, 1f);
            float top = Mathf.Min(goalSpeedKmh, car.maxSpeed * profile.topSpeedFactor);
            float want = top * Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(Mathf.Abs(angle) / 100f));
            bool brake = speed > want + 10f;

            stuckTicks = speed < 3f ? stuckTicks + 1 : 0;
            if (stuckTicks > SimulationDriver.SecondsToTicks(1.6f))
            {
                stuckTicks = 0;
                // Backing out did not free it last time (wedged on a hulk or a rock): put it back on the sand.
                if (tick - lastReverseTick < SimulationDriver.SecondsToTicks(6f)) { lastReverseTick = -100000; return new DriveCommand { Tick = tick, ResetEdge = true }; }
                lastReverseTick = tick;
                reverseUntilTick = tick + SimulationDriver.SecondsToTicks(1.1f);
            }
            return new DriveCommand
            {
                Tick = tick,
                Throttle = brake ? 0f : 1f,
                Steer = steer,
                Brake = brake && speed > want + 30f,
                Fire = gunner != null && gunner.WantsFire(tick)
            };
        }

        // Three rays ahead at bumper height: steer away from steep faces (rock walls, hulks, the fortress), not dunes.
        private float Whiskers(float speedKmh)
        {
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            float reach = 8f + speedKmh / 3.6f * 0.9f;
            float push = 0f;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * 24f, Vector3.up) * transform.forward;
                dir.y = 0f;
                if (!Physics.Raycast(origin, dir.normalized, out var hit, reach, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y > 0.6f || hit.rigidbody != null && hit.rigidbody.GetComponent<CarController>() != null) continue;
                float k = 1f - hit.distance / reach;
                push += (i == 0 ? (Vector3.Dot(hit.normal, transform.right) >= 0f ? 1f : -1f) : -i) * k * 1.4f;
            }
            return push;
        }

        private void Plan()
        {
            var boss = mission.Boss;
            Vector3 pos = transform.position;
            if (side == Side.Escort) { PlanEscort(boss, pos); return; }

            var order = mission.SquadTarget;
            if (order != null)
            {
                Vector3 station = order.transform.localPosition;
                float flank = station.x == 0f ? orbitSign : Mathf.Sign(station.x);
                Vector3 local = order.Role == BossRole.Core
                    ? new Vector3(flank * (6f + slot * 2f), 0f, -66f)
                    : new Vector3(flank * (56f + slot * 1.2f), 0f, station.z);
                Vector3 lane = boss.transform.TransformPoint(local);
                // Pass along the firing lane so the turret keeps its traverse and the car avoids the hull.
                goal = (pos - lane).sqrMagnitude > 18f * 18f ? lane
                    : lane + boss.transform.forward * (order.Role == BossRole.Core ? 12f : 20f);
                goalSpeedKmh = (pos - lane).sqrMagnitude > 28f * 28f ? 85f : 30f;
                KeepOffBossFront(boss);
                return;
            }

            // 1) An escort vehicle close by: drive past it on its flank (the turret does the work).
            var enemyCar = mission.NearestCar(pos, Team.Enemy, 75f);
            if (enemyCar != null)
            {
                goal = enemyCar.transform.position + enemyCar.transform.right * (orbitSign * 7f) + enemyCar.transform.forward * 6f;
                goalSpeedKmh = 95f;
                KeepOffBossFront(boss);
                return;
            }
            // 2) Troopers and turrets near us: run troopers over, strafe past turrets.
            var unit = mission.NearestGroundUnit(pos, 55f);
            if (unit != null)
            {
                Vector3 at = unit.transform.position;
                goal = unit.Kind == UnitKind.Turret ? at + Vector3.Cross(Vector3.up, (at - pos).normalized) * (orbitSign * 14f) : at + (at - pos).normalized * 6f;
                goalSpeedKmh = 80f;
                KeepOffBossFront(boss);
                return;
            }
            // 3) Circle the fortress, away from the wheel, keeping speed.
            if (boss != null && !boss.Defeated)
            {
                Vector3 centre = boss.transform.position - boss.transform.forward * 14f;   // biased to the stern, where the core is
                Vector3 from = pos - centre; from.y = 0f;
                float radius = 36f + slot * 3f;
                float a = Mathf.Atan2(from.x, from.z) + orbitSign * 0.55f;
                Vector3 g = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                Vector3 localGoal = boss.transform.InverseTransformPoint(g);
                if (localGoal.z > BossCrawler.Length * 0.3f && Mathf.Abs(localGoal.x) < radius * 0.7f)
                {
                    orbitSign = -orbitSign;   // about to cross the wheel's path: turn back along the flank
                    a = Mathf.Atan2(from.x, from.z) + orbitSign * 0.55f;
                    g = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                }
                goal = g;
                goalSpeedKmh = 78f;
                KeepOffBossFront(boss);
                return;
            }
            goal = mission.Map != null ? mission.Map.Town : pos + transform.forward * 20f;
            goalSpeedKmh = 50f;
        }

        private void PlanEscort(BossCrawler boss, Vector3 pos)
        {
            Vector3 anchor = boss != null && !boss.Defeated ? boss.transform.position : pos;
            var prey = mission.NearestCar(pos, Team.Players, 85f);
            if (prey != null && (prey.transform.position - anchor).sqrMagnitude < 170f * 170f)
            {
                Vector3 v = prey.Body != null ? prey.Body.linearVelocity : Vector3.zero;
                goal = prey.transform.position + v * 0.8f + prey.transform.right * (orbitSign * 6f);   // pass alongside and shoot
                goalSpeedKmh = 100f;
                return;
            }
            if (boss == null || boss.Defeated) { goal = pos + transform.forward * 20f; goalSpeedKmh = 30f; return; }
            // Patrol loop around the fortress, a little ahead of it.
            Vector3 from = pos - anchor; from.y = 0f;
            float a = Mathf.Atan2(from.x, from.z) + orbitSign * 0.4f;
            goal = anchor + boss.transform.forward * 20f + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (48f + slot % 3 * 6f);
            goalSpeedKmh = 55f;
        }

        // Never aim at a point in front of the bucket wheel: slide it out to the side of the fortress.
        private void KeepOffBossFront(BossCrawler boss)
        {
            if (boss == null || boss.Defeated) return;
            Vector3 local = boss.transform.InverseTransformPoint(goal);
            if (local.z < BossCrawler.Length * 0.5f - 4f || local.z > BossCrawler.Length * 0.5f + 30f || Mathf.Abs(local.x) > BossCrawler.Width * 0.5f + 6f) return;
            local.x = (local.x >= 0f ? 1f : -1f) * (BossCrawler.Width * 0.5f + 12f);
            goal = boss.transform.TransformPoint(local);
        }
    }
}
