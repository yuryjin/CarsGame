using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Economy;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    // Drives the production chassis with ordinary throttle, steering, brakes and the primary weapon.
    public sealed class TrainRouteTestDriver : ICarInputSource
    {
        private readonly CarController car;
        private readonly TrainRoute route;
        private readonly Vector3[] wash;
        private int segment = 1;

        public TrainRouteTestDriver(CarController car, TrainRoute route, bool viaWash)
        {
            this.car = car;
            this.route = route;
            if (!viaWash) return;
            Vector3 FromStart(float x, float z) => route.StartPose.position + route.StartPose.rotation * new Vector3(x, 0, z);
            wash = new[] { FromStart(0, 0), FromStart(0, 125), FromStart(12, 163), FromStart(42, 227),
                FromStart(37, 310), FromStart(17, 394), FromStart(30, 484), FromStart(54, 574), FromStart(40, 678),
                FromStart(18, 782), route.SwitchPoint, FromStart(1, 893), FromStart(9, 936),
                FromStart(48, 986), FromStart(83, 1035), route.StationPoint };
        }

        public DriveCommand Sample(int tick)
        {
            var position = car.transform.position;
            Vector3 target;
            float wanted;
            if (wash != null)
            {
                Vector3 end = wash[segment], leg = end - wash[segment - 1];
                float along = Vector3.Dot(position - wash[segment - 1], leg.normalized);
                if (segment < wash.Length - 1 && along >= leg.magnitude - 10f)
                {
                    segment++;
                    end = wash[segment];
                    leg = end - wash[segment - 1];
                    along = Vector3.Dot(position - wash[segment - 1], leg.normalized);
                }
                target = wash[segment - 1] + leg.normalized * Mathf.Clamp(along + 17f, 0f, leg.magnitude);
                float remaining = Vector3.Distance(position, route.StationPoint);
                wanted = segment == wash.Length - 1 ? Mathf.Clamp((remaining - 2f) * 0.55f, 0f, 26f) : 26f;
            }
            else
            {
                var tail = route.Tail;
                Vector3 relative = tail.InverseTransformPoint(position);
                float gap = -relative.z;
                // Sight along the road through the blind bend; pointing at the train cuts through the bluff.
                float look = Mathf.Clamp(12f + Mathf.Abs(car.SpeedKmh) * 0.10f, 14f, 22f);
                Pose lane = route.AlongsideRoad(position, look);
                target = lane.position + lane.rotation * Vector3.right;
                wanted = Mathf.Clamp(route.Velocity.magnitude + gap * 0.64f, 6f, 32f);
            }
            Vector3 local = car.transform.InverseTransformDirection(target - position);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float speed = car.SpeedKmh / 3.6f;
            float steer = Mathf.Clamp(angle / 23f - car.Body.angularVelocity.y * 0.18f, -1f, 1f);
            float error = wanted - speed;
            return new DriveCommand { Tick = tick, Throttle = Mathf.Clamp01(error * 0.65f),
                Steer = steer, Brake = error < -1.1f || wanted < 0.4f, Fire = wash == null };
        }
    }

    public sealed class TrainRoutePlayTests
    {
        private float beforeCapture;
        private string beforeSave, previousBody;
        private GameFlow flow;
        private RaceSpawner spawner;

        [SetUp] public void Setup()
        {
            beforeCapture = Time.captureDeltaTime;
            beforeSave = null;
            flow = null;
            spawner = null;
        }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (spawner != null) spawner.playerBodyId = previousBody;
            if (beforeSave != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(beforeSave, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll();
            Time.captureDeltaTime = beforeCapture;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator WashBeatsTrainToSwitchAndReachesStoppedTail() => Drive(true);

        [UnityTest, Timeout(300000)]
        public IEnumerator StarterCarCanHoldPositionNextToMovingPlatform() => Drive(false);

        [UnityTest]
        public IEnumerator RemovingTheRouteReleasesGeometryAndKeepsBorrowedSceneryMaterial()
        {
            var owner = new GameObject("FirstCanyon");
            var otherOwner = new GameObject("SecondCanyon");
            var car = new GameObject("TrophyCarrier", typeof(BoxCollider));
            try
            {
                var route = owner.AddComponent<TrainRoute>();
                route.Build(null);
                var wagon = route.Locomotive.GetComponentInChildren<MeshFilter>();
                Mesh wagonMesh = wagon.sharedMesh;
                Material localMaterial = wagon.GetComponent<MeshRenderer>().sharedMaterial;
                var wall = route.Locomotive.parent.Find("SandstoneBluffs_0").GetComponent<MeshCollider>();
                Mesh wallMesh = wall.sharedMesh;
                Physics.SyncTransforms();
                for (float d = 0; d < 1600f; d += 7f)
                {
                    Pose road = route.AlongsideRoad(route.StartPose.position, d);
                    for (int side = -1; side <= 1; side++)
                    {
                        var above = road.position + road.rotation * Vector3.right * (side * 8f) + Vector3.up * 75f;
                        Assert.IsFalse(wall.Raycast(new Ray(above, Vector3.down), out _, 80f),
                            $"Canyon rock blocks the railway road at {d} m, side {side}.");
                    }
                }
                var ramp = route.Tail.Find("SideRamp").GetComponent<BoxCollider>();
                var other = otherOwner.AddComponent<TrainRoute>();
                other.Build(localMaterial);
                var secondWagon = other.Locomotive.GetComponentInChildren<MeshFilter>().sharedMesh;
                var cup = route.Trophy;
                var cupMesh = cup.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(cup.parent, Is.EqualTo(route.Tail));
                route.CollectTrophy(car.transform);
                route.CollectTrophy(otherOwner.transform);
                Assert.That(cup.parent, Is.EqualTo(car.transform));
                Assert.That(cup.localScale.x, Is.LessThan(1f));
                Assert.That(car.transform.childCount, Is.EqualTo(1));
                Object.Destroy(otherOwner);
                yield return null; yield return null;
                Assert.That(secondWagon == null, Is.True);
                Assert.That(localMaterial != null && wagonMesh != null && ramp != null, Is.True);
                Object.Destroy(owner);
                yield return null; yield return null;
                Assert.That(localMaterial == null, Is.True);
                Assert.That(wagonMesh == null && wallMesh == null && wall == null && ramp == null, Is.True);
                Assert.That(cup == null && cupMesh == null && car.transform.childCount == 0, Is.True);
            }
            finally
            {
                if (owner != null) Object.Destroy(owner);
                if (otherOwner != null) Object.Destroy(otherOwner);
                if (car != null) Object.Destroy(car);
            }
        }

        private IEnumerator Drive(bool viaWash)
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(PlatformGlue.IsReady, Is.True);
            yield return null;
            beforeSave = JsonUtility.ToJson(PlatformGlue.Save.Data);
            PlatformGlue.Save.Data.progress.upgrades = new int[3];
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            spawner = race.spawner;
            previousBody = spawner.playerBodyId;
            spawner.playerBodyId = CarBodies.DefaultId;
            var normal = System.Array.Find(flow.tiers, t => t != null && t.tierId == "normal");
            flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m2"), normal);
            var mission = TrainMission.Current;
            var route = mission.Route;
            var car = race.Player.Car;
            Assert.That(car.GetComponent<CarBody>().BodyId, Is.EqualTo(CarBodies.DefaultId));
            car.SetInputSource(new TrainRouteTestDriver(car, route, viaWash));
            var atCountdown = route.Tail.position;
            while (race.State == RaceState.Countdown) yield return null;
            Assert.That(Vector3.Distance(atCountdown, route.Tail.position), Is.LessThan(0.5f));
            bool sawBend = false;
            var canyonView = route.SwitchPoint + new Vector3(10f, 6f, -426f);
            Assert.That(Physics.Raycast(canyonView, Vector3.left, out var face, 90f, 1 << LayerMask.NameToLayer("Track")), Is.True);
            Assert.That(face.collider.name, Does.StartWith("SandstoneBluffs"));
            int started = SimulationDriver.Instance.Tick;
            float maxRailOffset = 0f;
            int driverSwitch = -1, trainSwitch = -1, hold = 0, maxHold = 0, previous = started;
            while (race.State == RaceState.Racing && SimulationDriver.Instance.Tick - started < 5500)
            {
                yield return null;
                int now = SimulationDriver.Instance.Tick;
                maxRailOffset = Mathf.Max(maxRailOffset, Vector3.Distance(car.transform.position, route.AlongsideRoad(car.transform.position, 0f).position));
                if (Quaternion.Angle(route.Locomotive.rotation, route.Tail.rotation) > 3f) sawBend = true;
                if (mission.Diverted && driverSwitch < 0) driverSwitch = now - started;
                if (route.PassedSwitch && trainSwitch < 0) trainSwitch = now - started;
                hold = route.InCaptureZone(car.transform.position) && car.SpeedKmh > 30 ? hold + now - previous : 0;
                maxHold = Mathf.Max(maxHold, hold);
                previous = now;
                if (!viaWash && maxHold >= SimulationDriver.SecondsToTicks(3f)) break;
            }
            string Stamp(int ticks) => ticks < 0 ? "n/a" : $"{SimulationDriver.TicksToSeconds(ticks):F2}s";
            float elapsed = SimulationDriver.TicksToSeconds(SimulationDriver.Instance.Tick - started);
            Debug.Log($"[TrainRouteDrive] route={(viaWash ? "wash" : "platform")} elapsed={elapsed:F2}s " +
                $"driverSwitch={Stamp(driverSwitch)} trainSwitch={Stamp(trainSwitch)} " +
                $"alongside={maxHold * SimulationDriver.FixedStep:F2}s railRoadOffsetMax={maxRailOffset:F1}m player={car.transform.position:F1} tail={route.Tail.position:F1} outcome={mission.Outcome}");
            Assert.That(race.Player.TryGetRespawnPose(out var safe, out var facing), Is.True);
            Assert.That(Vector3.Dot(facing * Vector3.up, Vector3.up), Is.GreaterThan(0.99f));
            Assert.That(Physics.Raycast(safe + Vector3.up, Vector3.down, out var road, 3f, 1 << LayerMask.NameToLayer("Track")), Is.True);
            Assert.That(road.collider.name, Does.StartWith("DirtRoads"));
            if (viaWash)
            {
                Assert.That(maxRailOffset, Is.GreaterThan(100f), "The wash leaves the railway and cuts through the canyon.");
                Assert.That(driverSwitch, Is.GreaterThan(0));
                Assert.That(trainSwitch, Is.GreaterThan(driverSwitch));
                Assert.That(route.AtStation, Is.True);
                Assert.That(sawBend, Is.True, "Wagons turn independently as each crosses the bend.");
                Assert.That(mission.Outcome, Is.EqualTo(TrainOutcome.CapturedAtStation));
                Assert.That(Vector3.Distance(route.Tail.position, route.StationPoint), Is.InRange(9f, 13f));
            }
            else
            {
                Assert.That(maxRailOffset, Is.LessThan(18f), "The platform chase must stay on the railway side of the bluff.");
                Assert.That(maxHold, Is.GreaterThanOrEqualTo(SimulationDriver.SecondsToTicks(3f)));
            }
        }
    }
}
