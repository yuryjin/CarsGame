using System.Collections;
using NUnit.Framework;
using RacingGame.Core.Combat;
using RacingGame.Core.Economy;
using RacingGame.Core.Platform;
using RacingGame.Core.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class TrainEscortPlayTests
    {
        private GameFlow flow;
        private string savedProfile;
        private float originalDelta;

        [SetUp] public void Save() { originalDelta = Time.captureDeltaTime; }

        [TearDown] public void Restore()
        {
            if (flow != null) flow.EnterMenu();
            if (savedProfile != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(savedProfile, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll();
            Time.captureDeltaTime = originalDelta;
        }

        // The two drivers follow the canyon road and either line up a shot or pass on the outside.
        internal sealed class ChaseDriver : ICarInputSource
        {
            private readonly TrainMission mission;
            private readonly CarController car;
            private readonly TrainRouteTestDriver road;
            private readonly bool armed;
            public bool DropBack;

            public ChaseDriver(TrainMission mission, bool armed)
            {
                this.mission = mission;
                this.armed = armed;
                car = mission.Race.Player.Car;
                road = new TrainRouteTestDriver(car, mission.Route, false);
            }

            public DriveCommand Sample(int tick)
            {
                if (DropBack) return new DriveCommand { Tick = tick, Brake = true };
                var route = mission.Route;
                var tail = route.Tail;
                var position = car.Body.position;
                var command = road.Sample(tick);
                var guard = mission.Escort.Guard;
                bool attack = armed && guard.enabled && !guard.GetComponent<CarHealth>().IsDead;
                command.Fire = attack && car.Cannon.TurretTarget == guard;
                float targetZ = attack ? Vector3.Dot(guard.Body.position - tail.position, tail.forward) - 19f : 5.5f;
                var relative = tail.InverseTransformPoint(position);
                if (!armed && relative.z > -19f && relative.z < 10f)
                {
                    command.Steer = Mathf.Clamp(command.Steer + (15.3f - relative.x) * 0.08f, -1f, 1f);
                }
                float desired = Mathf.Clamp(route.Velocity.magnitude + (targetZ - relative.z) * 0.64f, 4f, 32f);
                float error = desired - car.SpeedKmh / 3.6f;
                command.Throttle = Mathf.Clamp01(error * 0.65f);
                command.Brake = error < -1.1f;
                return command;
            }
        }

        [UnityTest, Timeout(240000)] public IEnumerator OvertakingALiveGuardRecoversTheTrophy() => Chase(false);
        [UnityTest, Timeout(240000)] public IEnumerator TheStockCannonClearsTheRampAndCreditsOneKill() => Chase(true);

        private IEnumerator Chase(bool armed)
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            savedProfile = JsonUtility.ToJson(PlatformGlue.Save.Data);
            PlatformGlue.Save.Data.progress = new Core.Save.ProgressData();
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            race.spawner.playerBodyId = CarBodies.DefaultId;
            var tier = System.Array.Find(flow.tiers, entry => entry != null && entry.tierId == "normal");
            flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m2"), tier);
            var mission = TrainMission.Current;
            var escort = mission.Escort;
            var guard = escort.Guard;
            var health = guard.GetComponent<CarHealth>();
            Assert.AreEqual(1, race.Participants.Count);
            Assert.AreEqual(BodyClass.Buggy, guard.GetComponent<CarBody>().Class);
            Assert.AreEqual(Team.Enemy, Teams.Of(guard.gameObject));
            Assert.IsTrue(guard.Body.isKinematic);
            Assert.IsFalse(escort.ApproachClear);
            int shellHits = 0;
            health.HitResolved += (hit, result) =>
            {
                if (hit.Attacker == race.Player.gameObject && hit.HitType == HitType.Projectile && result.DamageApplied) shellHits++;
                if (result.Fatal) Debug.Log($"[TrainEscort] guard wrecked by {hit.HitType} from {hit.Attacker}");
            };
            var driver = new ChaseDriver(mission, armed);
            race.Player.Car.SetInputSource(driver);
            while (race.State == RaceState.Countdown) yield return null;

            int start = SimulationDriver.Instance.Tick;
            int release = -1, landing = -1, lastCaptureStart = -1;
            bool checkedPause = false, droppedBack = false, resetHold = false, blockedAtTail = false;
            float maxLaneError = 0f;
            var camera = Camera.main;
            int scenery = LayerMask.GetMask("Track", "Ground");
            bool sawDescent = false;
            while (race.State == RaceState.Racing && SimulationDriver.Instance.Tick - start < 4600)
            {
                int tick = SimulationDriver.Instance.Tick - start;
                var local = mission.Route.Tail.InverseTransformPoint(guard.Body.position);
                var playerAt = race.Player.Car.Body.position;
                var roadAt = mission.Route.AlongsideRoad(playerAt, 0f).position;
                maxLaneError = Mathf.Max(maxLaneError, Vector3.ProjectOnPlane(playerAt - roadAt, Vector3.up).magnitude);
                blockedAtTail |= escort.PlayerAtTail && !escort.ApproachClear && !health.IsDead;
                if (!escort.Released)
                    Assert.That(Mathf.Abs(local.z + 2f), Is.LessThan(1.5f), "the parked buggy must travel on the deck");
                else if (release < 0) release = tick;
                if (escort.Released && !sawDescent && landing < 0)
                {
                    Vector3 seat = guard.Body.position + Vector3.up * 0.75f;
                    Vector3 screen = camera.WorldToViewportPoint(seat);
                    sawDescent = screen.z > 0f && screen.x > 0.05f && screen.x < 0.95f && screen.y > 0.05f && screen.y < 0.95f
                        && !Physics.Linecast(camera.transform.position, seat, scenery, QueryTriggerInteraction.Ignore);
                }
                if (landing < 0 && guard.enabled && guard.IsGrounded && local.y < 0.75f && local.x > 7f)
                    landing = tick;
                if (mission.CaptureProgress > 0f && lastCaptureStart < 0) lastCaptureStart = tick;
                if (mission.CaptureProgress == 0f) lastCaptureStart = -1;
                if (armed && !droppedBack && mission.CaptureProgress > 0.18f)
                {
                    driver.DropBack = droppedBack = true;
                }
                if (driver.DropBack && !escort.PlayerAtTail)
                {
                    Assert.AreEqual(0f, mission.CaptureProgress);
                    driver.DropBack = false;
                    resetHold = true;
                }
                if (!checkedPause && mission.CaptureProgress >= 0.35f)
                {
                    var at = guard.Body.position;
                    var tailAt = mission.Route.Tail.position;
                    float progress = mission.CaptureProgress;
                    PauseController.Set(PauseReason.User, true);
                    for (int frame = 0; frame < 16; frame++) yield return null;
                    Assert.AreEqual(at, guard.Body.position);
                    Assert.AreEqual(tailAt, mission.Route.Tail.position);
                    Assert.AreEqual(progress, mission.CaptureProgress);
                    PauseController.Set(PauseReason.User, false);
                    checkedPause = true;
                }
                if (tick % (tick < 1000 ? 100 : 500) == 0)
                    Debug.Log($"[TrainEscort] armed={armed} tick={tick} guardLocal={local:F1} playerLocal={mission.Route.Tail.InverseTransformPoint(race.Player.Car.Body.position):F1} speed={race.Player.Car.SpeedKmh:F0}/{guard.SpeedKmh:F0} hp={health.Health} clear={escort.ApproachClear} hold={mission.CaptureProgress:F2}");
                yield return null;
            }
            int finish = SimulationDriver.Instance.Tick - start;
            int holdTicks = lastCaptureStart >= 0 ? finish - lastCaptureStart + 1 : 0;
            Debug.Log($"[TrainEscort] armed={armed} release={release * 0.02f:F2}s landed={landing * 0.02f:F2}s visible={sawDescent} holdTicks={holdTicks} finish={finish * 0.02f:F2}s laneErrorMax={maxLaneError:F1}m outcome={mission.Outcome} shellHits={shellHits} kills={race.Player.Kills}");
            Assert.Greater(release, 0);
            Assert.That(landing - release, Is.InRange(1, 170), "the buggy must actually land down the side ramp");
            Assert.IsTrue(sawDescent, "the driver must see the buggy descend from the moving train");
            Assert.AreEqual(TrainOutcome.CapturedOnTrain, mission.Outcome);
            Assert.AreEqual(SimulationDriver.SecondsToTicks(TrainMission.HoldSeconds), holdTicks);
            Assert.Less(maxLaneError, 20f, "this run must stay on the railway road through the canyon fork");
            Assert.IsTrue(checkedPause);
            if (armed)
            {
                Assert.IsTrue(resetHold, "braking out of the physical capture zone resets the transfer");
                Assert.Greater(shellHits, 0);
            }
            else Assert.IsTrue(blockedAtTail, "the live buggy contested the approach before the overtake");
            Assert.AreEqual(armed, health.IsDead, "one route passes a live guard, the other uses the stock cannon");
            Assert.AreEqual(armed ? 1 : 0, race.Results.Player.Kills);
            Assert.AreEqual(armed ? 2 : 0, flow.LastStarsEarned & 2);
            Assert.AreEqual(armed ? 1 : 0, PlatformGlue.Save.Data.Kills);
            var frozen = guard.Body.position;
            int shots = guard.GetComponent<AiGunner>().ShotsRequested;
            Assert.IsTrue(guard.Body.isKinematic);
            Assert.IsFalse(guard.enabled);
            for (int frame = 0; frame < 24; frame++) yield return null;
            Assert.AreEqual(frozen, guard.Body.position);
            Assert.AreEqual(shots, guard.GetComponent<AiGunner>().ShotsRequested);
            flow.RetryRace();
            Assert.AreEqual(1, Object.FindObjectsByType<TrainEscort>(FindObjectsInactive.Exclude).Length);
            Assert.IsTrue(TrainMission.Current.Escort.Guard.Body.isKinematic);
            yield return null;
            Assert.IsTrue(guard == null, "the previous guard and colliders are gone after retry");
        }
    }
}
