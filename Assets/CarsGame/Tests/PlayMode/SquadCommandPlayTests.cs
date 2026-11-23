using System.Collections;
using NUnit.Framework;
using RacingGame.Core.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class SquadCommandPlayTests
    {
        private GameFlow flow;
        private float capture;
        private RaceEventDefinition original;
        private DifficultyTier tier;
        private MissionUnit lastHit;
        private GameObject ally;

        [SetUp] public void SetUp()
        {
            capture = Time.captureDeltaTime;
            original = RaceSetup.Event;
            tier = RaceSetup.Tier;
        }

        [TearDown] public void TearDown()
        {
            MissionUnit.Damaged -= Hit;
            if (flow != null) flow.EnterMenu();
            RaceSetup.Event = original;
            RaceSetup.Tier = tier;
            Time.captureDeltaTime = capture;
        }

        private sealed class ParkAndFire : ICarInputSource
        {
            public AiGunner Gunner;
            public DriveCommand Sample(int tick) => new DriveCommand { Tick = tick, Brake = true, Fire = Gunner.WantsFire(tick) };
        }

        private void Hit(MissionUnit unit, GameObject attacker, float damage)
        {
            if (attacker == ally) lastHit = unit;
        }

        private static void Select(BossPart part)
        {
            GameObject.Find("SquadCommand").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Mark" + part.Boss.Parts.IndexOf(part)).GetComponent<Button>().onClick.Invoke();
        }

        // Find a real position on the sand where the gun has an unobstructed firing lane to this station.
        private static void DriveIntoRange(CarController car, MissionDirector mission, BossPart part)
        {
            Vector3 aim = part.AimPoint;
            for (int step = 0; step < 36; step++)
                for (int range = 62; range >= 35; range -= 3)
                {
                    Vector3 from = aim + Quaternion.Euler(0f, step * 10f, 0f) * -mission.Boss.transform.forward * range;
                    Vector3 forward = aim - from;
                    mission.Map.GroundPose(from, forward, out var pos, out var rot);
                    Vector3 origin = pos + rot * new Vector3(0f, 1.6f, 2.5f);
                    Vector3 to = aim - origin;
                    if (to.magnitude > 69f || Mathf.Abs(Vector3.Angle(Vector3.up, to) - 90f) > 28f) continue;
                    if (!Physics.Raycast(origin, to.normalized, out var hit, to.magnitude + .5f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<MissionUnit>() != part) continue;
                    car.Teleport(pos + Vector3.up * .2f, rot);
                    Physics.SyncTransforms();
                    return;
                }
            Assert.Fail("No firing lane on the sand for " + part.Role);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AlliedShotsFollowCoreAndMortarOrdersThenFindAnotherEnemy()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m1"), flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var mission = MissionDirector.Current;
            var car = race.Participants[1].Car;
            ally = car.gameObject;
            var gunner = car.GetComponent<AiGunner>();
            car.SetInputSource(new ParkAndFire { Gunner = gunner });
            car.Weapons.SetPrimaryType(PrimaryWeaponType.Cannon);
            mission.Boss.Hold(SimulationDriver.Instance.Tick, 45f);
            MissionUnit.Damaged += Hit;
            var mortar = mission.Boss.Parts.Find(p => p.Role == BossRole.Mortar);
            foreach (var part in new[] { mission.Boss.Core, mortar })
            {
                Select(part);
                Assert.AreSame(part, mission.SquadTarget);
                DriveIntoRange(car, mission, part);
                lastHit = null;
                for (int i = 0; i < 400 && lastHit != part; i++) yield return null;
                Debug.Log($"[SquadOrder] target={part.Role} ally={car.CarId} actualHit={lastHit?.name} shots={gunner.ShotsRequested} range={Vector3.Distance(car.transform.position,part.AimPoint):F1}");
                Assert.AreSame(part, lastHit, "A physical allied projectile must hit the marked station");
            }
            mortar.TakeHit(race.Player.gameObject, 10000f, mortar.AimPoint);
            Assert.IsFalse(mortar.Alive);
            Assert.IsNull(mission.SquadTarget);
            mission.MarkSquadTarget(mortar);
            Assert.IsNull(mission.SquadTarget, "A destroyed part cannot be marked again");
            var site = mission.Map.RoutePoint(mission.Map.RouteLength * .55f);
            mission.Map.GroundPose(site, Vector3.forward, out var start, out var heading);
            car.Teleport(start, heading);
            var turret = GroundTurretUnit.Spawn(mission.Map, start + heading * Vector3.forward * 22f, -Vector3.forward);
            lastHit = null;
            for (int i = 0; i < 420 && lastHit != turret; i++) yield return null;
            Assert.AreSame(turret, lastHit, "Free fire resumes against a live hostile");
            Debug.Log("[SquadOrder] destroyed mark cleared; next target hit=" + lastHit.Kind);
        }
    }
}
