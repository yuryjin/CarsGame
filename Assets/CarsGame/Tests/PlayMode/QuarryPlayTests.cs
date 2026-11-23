using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class QuarryPlayTests
    {
        private float capture;
        private GameFlow flow;
        private string saved;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator StockBuggyAndSportsCarFinishBothWaysWithOrderedGates()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady); yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var normal = System.Array.Find(flow.tiers, t => t.tierId == "normal");
            var source = System.Array.Find(flow.events, e => e.eventId == "dirt");
            Assert.AreEqual(MountainFork.Quarry, source.mountainFork);
            var ev = Object.Instantiate(source);
            ev.aiCountOverride = 0;
            ev.pickupCount = 0;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            var upgrades = PlatformGlue.Save.Data.progress.upgrades;
            System.Array.Clear(upgrades, 0, upgrades.Length);
            var times = new double[2, 2];
            for (int body = 0; body < 2; body++)
            for (int route = 0; route < 2; route++)
            {
                string bodyId = body == 0 ? "classic" : "dune";
                bool branch = route == 1;
                RaceDirector.Instance.Reseed(12345);
                race.spawner.playerBodyId = bodyId;
                flow.StartRace(ev, normal);
                var racer = race.Player;
                Assert.AreEqual(bodyId, racer.GetComponent<CarBody>().BodyId);
                var ai = racer.gameObject.AddComponent<AiDriver>(); ai.Initialize(null, 0f);
                var input = racer.gameObject.AddComponent<ScriptedDriverInput>();
                racer.Car.SetInputSource(ai);
                var path = racer.Progress.Path;
                var fork = path.Fork;
                var car = racer.Car;
                float exit = fork.ApproachDistance + fork.MainSpan;
                Assert.IsTrue(fork.IsDirt);
                while (race.State == RaceState.Countdown) yield return null;
                int runStart = SimulationDriver.Instance.Tick, start = -1, end = -1;
                int dirtTicks = 0, previousGate = racer.Progress.GatesPassedThisLap, gateChanges = 0;
                bool inForkControls = false;
                float lastD = racer.Progress.Distance;
                car.Teleported += () => Debug.Log($"[QuarryRecovery] {bodyId} branch={branch} distance={lastD:F1} ticks={SimulationDriver.Instance.Tick - runStart} stuck={ai.StuckTime:F1}");
                var trace = new System.Collections.Generic.Queue<string>(12);
                string traceDirectory = System.Environment.GetEnvironmentVariable("CG_QUARRY_TRACE");
                var curve = new System.Text.StringBuilder("tick,mainMetres,forkMetres,offset,speedKmh,surface\n");
                for (int tick = 0; tick < 9000 && racer.Progress.LapsCompleted == 0; tick++)
                {
                    float d = racer.Progress.Distance; lastD = d;
                    if (d >= fork.ApproachDistance && start < 0) start = SimulationDriver.Instance.Tick;
                    if (d >= exit && start >= 0 && end < 0) end = SimulationDriver.Instance.Tick;
                    if (branch && d > fork.ApproachDistance - 27f && d < exit + 16f)
                    {
                        if (!inForkControls) { inForkControls = true; car.SetInputSource(input); }
                        fork.FollowShortcut(car, d, out float steer, out float throttle);
                        input.Set(throttle, steer);
                    }
                    else if (inForkControls) { inForkControls = false; car.SetInputSource(ai); }
                    if (car.Surface == GroundSurface.Ground && path.OnFork(car.transform.position)) dirtTicks++;
                    int passed = racer.Progress.GatesPassedThisLap;
                    if (passed != previousGate) { gateChanges++; previousGate = passed; }
                    if (branch && tick % 12 == 0 && start >= 0 && d < exit + 80f)
                    {
                        float branchD = fork.Project(car.transform.position, out float error);
                        if (!string.IsNullOrEmpty(traceDirectory))
                            curve.AppendLine(System.FormattableString.Invariant($"{SimulationDriver.Instance.Tick - runStart},{d:F2},{branchD:F2},{Mathf.Sqrt(error):F2},{car.SpeedKmh:F2},{car.Surface}"));
                        if (trace.Count == 12) trace.Dequeue();
                        float lateral = path.Spline.LateralOffset(car.transform.position, d);
                        trace.Enqueue($"{d:F1} lateral={lateral:F1} v={car.SpeedKmh:F1} y={car.transform.position.y:F1} {car.Surface} steer={car.LastCommand.Steer:F2}");
                    }
                    Assert.AreEqual(0, racer.AutoResets, $"{bodyId} branch={branch} d={d:F1}, {string.Join(" | ", trace)}");
                    yield return null;
                }
                if (!string.IsNullOrEmpty(traceDirectory) && branch)
                    System.IO.File.WriteAllText(System.IO.Path.Combine(traceDirectory, bodyId + "-dirt.csv"), curve.ToString());
                float segment = (end - start) * SimulationDriver.FixedStep;
                float lap = (SimulationDriver.Instance.Tick - runStart) * SimulationDriver.FixedStep;
                Debug.Log($"[Quarry] body={bodyId} branch={branch} segment={segment:F2}s lap={lap:F2}s gates={gateChanges} dirtTicks={dirtTicks} shortcuts={racer.Shortcuts} resets={racer.Resets} d={racer.Progress.Distance:F1} pos={car.transform.position}");
                Assert.AreEqual(1, racer.Progress.LapsCompleted, bodyId);
                Assert.GreaterOrEqual(gateChanges, racer.Progress.gateCount - 1);
                Assert.AreEqual(0, racer.Resets, "whole lap from the grid without teleporting");
                Assert.Greater(end, start); Assert.Greater(start, 0);
                if (branch) { Assert.Greater(dirtTicks, 100); Assert.AreEqual(1, racer.Shortcuts); }
                else Assert.AreEqual(0, racer.Shortcuts);
                times[body, route] = segment;
                flow.EnterMenu(); yield return null;
            }
            Assert.Less(times[1, 1], times[0, 1], "the lighter buggy carries more speed along loose dirt");
            Assert.Greater(times[1, 0] / times[1, 1], times[0, 0] / times[0, 1], "the shortcut is relatively more attractive to the buggy");
            Object.Destroy(ev);
        }

    }
}
