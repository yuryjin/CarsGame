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
        [UnityTest, Timeout(240000)]
        public IEnumerator SquadDrivesFromItsStartingFormationToEngageTheMarkedCore()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m1"), flow.tiers[0]);
            var mission = MissionDirector.Current;
            var core = mission.Boss.Core;
            Select(core);
            int hitCar = 0;
            void OnHit(MissionUnit target, GameObject source, float amount)
            {
                if (target != core || source == null) return;
                var shooter = source.GetComponent<RaceParticipant>();
                if (shooter != null && !shooter.isPlayer && Teams.Of(source) == Team.Players) hitCar = shooter.Car.CarId;
            }
            MissionUnit.Damaged += OnHit;
            try
            {
                while (race.State == RaceState.Countdown) yield return null;
                float startRange = Vector3.Distance(race.Participants[1].transform.position, core.AimPoint);
                int step = 0;
                for (; step < 3300 && hitCar == 0 && mission.Result == MissionDirector.Outcome.None; step++) yield return null;
                float closest = float.MaxValue;
                foreach (var p in race.Participants) if (!p.isPlayer) closest = Mathf.Min(closest, Vector3.Distance(p.transform.position, core.AimPoint));
                Debug.Log($"[SquadApproach] start={startRange:F0}m nearest={closest:F0}m alliedShooter={hitCar} ticks={step}");
                Assert.Greater(hitCar, 0, "An unassisted teammate should drive into range and land a shot");
            }
            finally { MissionUnit.Damaged -= OnHit; }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BriefingAndLiveMapTrackTheDroneHangarAndIndependentMortar()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            Object.FindAnyObjectByType<MenuScreens>().Show("events");
            GameObject.Find("Start_m1").GetComponent<Button>().onClick.Invoke();
            var brief = GameObject.Find("ModuleBriefing");
            Assert.NotNull(brief, "The player can inspect the fortress before the countdown");
            Assert.IsNull(MissionDirector.Current);
            Assert.AreEqual(9, brief.GetComponentsInChildren<Button>().Length);
            Assert.AreEqual(Loc.Get("mission.module_functions"), brief.transform.Find("ModuleFunctions").GetComponent<TMPro.TMP_Text>().text);
            Button hangarButton = System.Array.Find(brief.GetComponentsInChildren<Button>(), b => b.GetComponentInChildren<TMPro.TMP_Text>().text == Loc.Get("mission.part.hangar"));
            hangarButton.onClick.Invoke();
            GameObject.Find("Deploy").GetComponent<Button>().onClick.Invoke();
            var mission = MissionDirector.Current;
            Assert.AreEqual(BossRole.Hangar, mission.SquadTarget.Role);
            var boss = mission.Boss;
            var hangar = mission.SquadTarget;
            var mortar = boss.Parts.Find(p => p.Role == BossRole.Mortar);
            // Observe the usual spawn/strike callbacks and forward them to the real game.
            var launch = BossPart.RequestDrone;
            var strike = BossPart.RequestStrike;
            int launched = 0, shelled = 0;
            BossPart.RequestDrone = (station, tick) =>
            {
                bool success = launch(station, tick);
                if (success && station == hangar) launched++;
                return success;
            };
            BossPart.RequestStrike = (station, target, tick) =>
            {
                strike(station, target, tick);
                if (station == mortar) shelled++;
            };
            try
            {
                while (race.State == RaceState.Countdown) yield return null;
                mission.Map.GroundPose(boss.transform.position - boss.transform.forward * 100f, boss.transform.forward, out var pos, out var rot);
                race.Player.Car.Teleport(pos, rot);
                for (int i = 0; i < 550 && (launched == 0 || shelled == 0); i++) yield return null;
                Assert.Greater(launched, 0, "The live hangar launches a drone");
                Assert.Greater(shelled, 0, "The live mortar calls a marked strike");
                int beforeDrones = launched, beforeStrikes = shelled;
                GameObject.Find("SquadCommand").GetComponent<Button>().onClick.Invoke();
                var liveHangar = GameObject.Find("Mark" + boss.Parts.IndexOf(hangar)).GetComponent<Button>();
                var liveMortar = GameObject.Find("Mark" + boss.Parts.IndexOf(mortar)).GetComponent<Button>();
                hangar.TakeHit(race.Player.gameObject, 10000f, hangar.AimPoint);
                Assert.IsFalse(liveHangar.interactable);
                Assert.IsTrue(liveHangar.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("×"));
                Assert.IsTrue(liveMortar.interactable);
                for (int i = 0; i < 825; i++) yield return null;
                Assert.AreEqual(beforeDrones, launched, "The destroyed hangar cannot launch after a full launch interval");
                Assert.Greater(shelled, beforeStrikes);
                Debug.Log($"[BossModules] hangar launches {beforeDrones}→{launched}, mortar strikes {beforeStrikes}→{shelled}");
            }
            finally { BossPart.RequestDrone = launch; BossPart.RequestStrike = strike; }
        }
    }
}
