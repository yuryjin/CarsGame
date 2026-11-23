using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class IceStationPlayTests
    {
        private GameFlow flow;
        private float capture;
        private string saved;
        [SetUp]
        public void Setup()
        {
            capture = Time.captureDeltaTime;
        }
        [TearDown]
        public void Cleanup()
        {
            if (flow != null)
                flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator OneClassicFinishesBothRoutesAndDryGripReturnsAfterLeavingTheStation()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var normal = System.Array.Find(flow.tiers, t => t.tierId == "normal");
            var source = System.Array.Find(flow.events, ev => ev.eventId == "ice");
            Assert.AreEqual(MountainFork.IceStation, source.mountainFork);
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            var levels = PlatformGlue.Save.Data.progress.upgrades;
            System.Array.Clear(levels, 0, levels.Length);
            float[] entry = new float[2], segment = new float[2], meanSlip = new float[2];
            float[] residual = new float[3];
            var ev = Object.Instantiate(source);
            ev.aiCountOverride = 0;
            ev.pickupCount = 0;
            var ordinary = Object.Instantiate(System.Array.Find(flow.events, candidate => candidate.eventId == "e1"));
            ordinary.aiCountOverride = 0;
            ordinary.pickupCount = 0;
            for (int run = 0; run < 3; run++)
            {
                bool station = run < 2, tunnel = run == 1;
                race.spawner.playerBodyId = "classic";
                RaceDirector.Instance.Reseed(12345);
                flow.StartRace(station ? ev : ordinary, normal);
                var player = race.Player;
                var car = player.Car;
                var path = player.Progress.Path;
                var fork = path.Fork;
                var ai = player.gameObject.AddComponent<AiDriver>();
                ai.Initialize(null, 0f);
                var input = player.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(ai);
                while (race.State == RaceState.Countdown)
                    yield return null;
                if (station)
                {
                    Assert.NotNull(fork.transform.Find("StationIce_0"));
                    Assert.NotNull(fork.transform.Find("StationTunnel_0"));
                    int runStart = SimulationDriver.Instance.Tick, start = -1, end = -1;
                    int samples = 0, sampleWindow = 0, controls = 0, gateChanges = 0;
                    int previous = player.Progress.GatesPassedThisLap;
                    float slip = 0f;
                    for (int i = 0; i < 7800 && player.Progress.LapsCompleted == 0; i++)
                    {
                        float along = path.SignedDelta(fork.ApproachDistance, player.Progress.Distance);
                        if (start < 0 && along > -28f)
                        {
                            start = SimulationDriver.Instance.Tick;
                            entry[run] = car.SpeedKmh;
                        }
                        if (start >= 0 && along >= fork.MainSpan && end < 0)
                            end = SimulationDriver.Instance.Tick;
                        if (tunnel && along >= -27f && along < fork.MainSpan + 2f && end < 0)
                        {
                            if (controls == 0)
                            {
                                car.SetInputSource(input);
                                controls = 1;
                            }
                            fork.FollowShortcut(car, player.Progress.Distance, out var steer, out var throttle);
                            input.Set(throttle, steer);
                        }
                        else if (controls == 1)
                        {
                            controls = 2;
                            car.SetInputSource(ai);
                        }
                        if (along > fork.MainSpan * .29f && along < fork.MainSpan * .72f)
                        {
                            sampleWindow++;
                            if (car.Surface != GroundSurface.None)
                            {
                                Assert.AreEqual(tunnel ? GroundSurface.Track : GroundSurface.Ice, car.Surface, $"along={along:F1} pos={car.transform.position} speed={car.SpeedKmh:F1} onFork={path.OnFork(car.transform.position)}");
                                slip += Mathf.Abs(Vector3.Dot(car.Body.linearVelocity, car.transform.right));
                                samples++;
                            }
                        }
                        if (player.Progress.GatesPassedThisLap != previous)
                        {
                            previous = player.Progress.GatesPassedThisLap;
                            gateChanges++;
                        }
                        yield return null;
                    }
                    segment[run] = (end - start) * SimulationDriver.FixedStep;
                    meanSlip[run] = slip / Mathf.Max(1, samples);
                    Debug.Log($"[IceStation] covered={tunnel} entry={entry[run]:F2}km/h time={segment[run]:F2}s meanSlip={meanSlip[run]:F2}m/s contact={samples}/{sampleWindow} lap={(SimulationDriver.Instance.Tick - runStart) * SimulationDriver.FixedStep:F2}s gates={gateChanges}");
                    Assert.AreEqual(1, player.Progress.LapsCompleted);
                    Assert.AreEqual(0, player.Resets);
                    Assert.GreaterOrEqual(gateChanges, player.Progress.gateCount - 1);
                    Assert.Greater(end, start);
                    Assert.Greater(samples, 35);
                    Assert.Greater(samples, sampleWindow * .75f);
                    Assert.AreEqual(tunnel ? 1 : 0, player.Shortcuts);
                }
                // Matched side impulse on each physical surface isolates tyre grip from the route's radius.
                if (station)
                {
                    flow.StartRace(ev, normal);
                    player = race.Player;
                    car = player.Car;
                    path = player.Progress.Path;
                    fork = path.Fork;
                    while (race.State == RaceState.Countdown)
                        yield return null;
                    input = player.gameObject.AddComponent<ScriptedDriverInput>();
                }
                car.SetInputSource(input);
                input.Set(0f, 0f);
                Vector3 position;
                Quaternion rotation;
                if (tunnel)
                {
                    position = fork.Position(.50f);
                    rotation = Quaternion.LookRotation(fork.Position(.51f) - fork.Position(.49f));
                }
                else
                    path.Pose((TrackFork.Entry + TrackFork.Exit) * .5f, 0f, out position, out rotation);
                car.Teleport(position + Vector3.up * .3f, rotation);
                for (int i = 0; i < 20; i++)
                    yield return null;
                Assert.AreEqual(station && !tunnel ? GroundSurface.Ice : GroundSurface.Track, car.Surface);
                Vector3 right = car.transform.right;
                car.Body.linearVelocity = car.transform.forward * 10f + right * 3f;
                for (int i = 0; i < 8; i++)
                    yield return null;
                residual[run] = Mathf.Abs(Vector3.Dot(car.Body.linearVelocity, right));
                Debug.Log($"[IceStation] run={run} remainingSideSpeed={residual[run]:F3}m/s");
                flow.EnterMenu();
                yield return null;
            }
            Debug.Log($"[IceComparison] open/tunnel segment={segment[0]:F2}/{segment[1]:F2}s meanSlip={meanSlip[0]:F3}/{meanSlip[1]:F3}m/s");
            Assert.AreEqual(entry[0], entry[1], .4f);
            Assert.Greater(residual[0], residual[1] * 1.25f);
            Assert.AreEqual(residual[1], residual[2], .25f);
            Object.Destroy(ev);
            Object.Destroy(ordinary);
        }
    }
}
