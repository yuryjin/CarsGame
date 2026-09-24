using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Sim;
using RacingGame.Platform;

namespace RacingGame.Tests
{
    /// <summary>
    /// Counts side impacts against static scenery (barriers, walls, props): the number an AI
    /// driver should keep at zero on a clear lap.
    /// </summary>
    public sealed class WallImpactCounter : MonoBehaviour
    {
        public int Impacts;
        public float WorstSpeed;
        public readonly List<float> ImpactDistances = new List<float>();
        private RaceProgress progress;

        private void Awake() { progress = GetComponent<RaceProgress>(); }

        private void OnCollisionEnter(Collision c)
        {
            if (c.rigidbody != null || c.contactCount == 0) return;
            if (Mathf.Abs(c.GetContact(0).normal.y) > 0.5f) return;   // road, kerb, ramp
            float v = c.relativeVelocity.magnitude;
            if (v < 2f) return;
            Impacts++;
            WorstSpeed = Mathf.Max(WorstSpeed, v);
            if (progress != null && ImpactDistances.Count < 64) ImpactDistances.Add(progress.Distance);
        }
    }

    /// <summary>
    /// Drives the real scene with AI in every seat and reports how the field raced: lap times (in
    /// simulation ticks), grass time, wall impacts, deaths and resets. One frame = one 50 Hz tick
    /// (<see cref="Time.captureDeltaTime"/>), so a race runs as fast as the CPU allows.
    /// Knobs for tuning runs come from environment variables:
    /// CG_EVENT (e1), CG_TIER (normal), CG_AI (rival count, -1 = event default), CG_LAPS,
    /// CG_SEED (12345), CG_PARAMS ("field=value ..." applied to every AiDriver), CG_OUT (JSON path).
    /// </summary>
    public sealed class RaceHarness
    {
        private const int TickLimit = 50 * 600;

        [Serializable] public class CarReport
        {
            public string name; public bool player, finished;
            public int laps, deaths, kills, autoResets, aiResets, wallImpacts;
            public float offTrackSeconds, worstImpact, maxLateral, finishSeconds;
            public float distance, speed, desiredSpeed, stuckSeconds; public int nextGate;
            public float[] lapSeconds; public float[] impactAt;
        }
        [Serializable] public class Report
        {
            public string evt, tier, parameters; public int seed, ticks, shots, hits, mines, mineHits;
            public float raceSeconds; public CarReport[] cars;
        }

        private float savedCaptureDelta;
        [SetUp] public void SaveTimestep() { savedCaptureDelta = Time.captureDeltaTime; }
        [TearDown] public void RestoreTimestep() { Time.captureDeltaTime = savedCaptureDelta; LogAssert.ignoreFailingMessages = false; }

        private static string Env(string key, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator AutopilotRace()
        {
            string evtId = Env("CG_EVENT", "e1"), tierId = Env("CG_TIER", "normal"), parameters = Env("CG_PARAMS", "");
            int seed = int.Parse(Env("CG_SEED", "12345"), CultureInfo.InvariantCulture);
            int rivals = int.Parse(Env("CG_AI", "-1"), CultureInfo.InvariantCulture);
            int laps = int.Parse(Env("CG_LAPS", "0"), CultureInfo.InvariantCulture);

            LogAssert.ignoreFailingMessages = true;   // a measuring run, not a log-cleanliness check
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float wait = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < wait) yield return null;
            if (!PlatformGlue.IsReady)
                Debug.LogWarning($"[RaceHarness] not ready: save={(PlatformGlue.Save != null)} loaded={(PlatformGlue.Save != null && PlatformGlue.Save.Loaded)} loc={Loc.Ready} platform={(PlatformRuntime.Current != null ? PlatformRuntime.Current.IsInitialized.ToString() : "null")} scene={SceneManager.GetActiveScene().name} roots={SceneManager.GetActiveScene().rootCount} glue={UnityEngine.Object.FindObjectsByType<PlatformGlue>(FindObjectsInactive.Include).Length}/{UnityEngine.Object.FindAnyObjectByType<PlatformGlue>() != null} flow={UnityEngine.Object.FindAnyObjectByType<GameFlow>() != null} t={Time.frameCount}");
            Assert.IsTrue(PlatformGlue.Save != null && PlatformGlue.Save.Loaded, "save never loaded");

            var flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
            var race = UnityEngine.Object.FindAnyObjectByType<RaceManager>();
            var bus = UnityEngine.Object.FindAnyObjectByType<DebugBus>();
            RaceEventDefinition ev = null; DifficultyTier tier = null;
            foreach (var e in flow.events) if (e != null && e.eventId == evtId) ev = e;
            foreach (var t in flow.tiers) if (t != null && t.tierId == tierId) tier = t;
            Assert.IsNotNull(ev, "event " + evtId);
            ev = UnityEngine.Object.Instantiate(ev);
            if (rivals >= 0) ev.aiCountOverride = rivals;
            if (laps > 0) ev.laps = laps;

            var director = RaceDirector.Instance;
            director.raceSeed = seed;
            typeof(RaceDirector).GetProperty("Rng").SetValue(director, new RngStreams(seed));
            flow.StartRace(ev, tier);
            bus.Execute("autopilot", "on");

            var drivers = new List<RaceParticipant>(race.Participants);
            foreach (var p in drivers)
            {
                var ai = p.GetComponent<AiDriver>();
                if (ai != null) Apply(ai, parameters);
                if (p.GetComponent<WallImpactCounter>() == null) p.gameObject.AddComponent<WallImpactCounter>();
            }

            var lapTicks = new Dictionary<RaceParticipant, List<int>>();
            var lastLaps = new Dictionary<RaceParticipant, int>();
            var off = new Dictionary<RaceParticipant, float>();
            var maxLat = new Dictionary<RaceParticipant, float>();
            var finishTick = new Dictionary<RaceParticipant, int>();
            foreach (var p in drivers) { lapTicks[p] = new List<int>(); lastLaps[p] = 0; off[p] = 0f; maxLat[p] = 0f; }

            while (race.State == RaceState.Countdown) yield return null;
            int startTick = SimulationDriver.Instance.Tick;
            foreach (var p in drivers) lapTicks[p].Add(startTick);

            int lastTick = startTick;
            while (race.State == RaceState.Racing || race.State == RaceState.Finishing)
            {
                yield return null;
                int tick = SimulationDriver.Instance.Tick;
                float dt = (tick - lastTick) * SimulationDriver.FixedStep; lastTick = tick;
                foreach (var p in drivers)
                {
                    if (p == null || p.Progress == null || p.Progress.Path == null) continue;
                    if (p.Car.Surface == GroundSurface.Ground) off[p] += dt;
                    float lat = Mathf.Abs(p.Progress.Path.Spline.LateralOffset(p.transform.position, p.Progress.Distance));
                    if (p.Health == null || !p.Health.IsDead) maxLat[p] = Mathf.Max(maxLat[p], Mathf.Min(lat, 99f));
                    int l = p.Progress.LapsCompleted;
                    if (l > lastLaps[p]) { lapTicks[p].Add(tick); lastLaps[p] = l; }
                    if (!finishTick.ContainsKey(p) && race.HasFinished(p)) finishTick[p] = tick;
                }
                if (tick - startTick > TickLimit) break;
            }

            var report = new Report
            {
                evt = evtId, tier = tierId, parameters = parameters, seed = seed,
                ticks = SimulationDriver.Instance.Tick - startTick,
                shots = director.ShotsFired, hits = director.ShellHits,
                mines = director.MinesDropped, mineHits = director.MineHits
            };
            report.raceSeconds = report.ticks * SimulationDriver.FixedStep;
            var cars = new List<CarReport>();
            foreach (var p in drivers)
            {
                var ai = p.GetComponent<AiDriver>();
                var wc = p.GetComponent<WallImpactCounter>();
                var ticks = lapTicks[p];
                var lapsSec = new float[Mathf.Max(0, ticks.Count - 1)];
                for (int i = 1; i < ticks.Count; i++) lapsSec[i - 1] = (ticks[i] - ticks[i - 1]) * SimulationDriver.FixedStep;
                cars.Add(new CarReport
                {
                    name = p.name, player = p.isPlayer, finished = finishTick.ContainsKey(p),
                    finishSeconds = finishTick.TryGetValue(p, out int ft) ? (ft - startTick) * SimulationDriver.FixedStep : -1f,
                    laps = p.Progress.LapsCompleted, deaths = p.Deaths, kills = p.Kills, autoResets = p.AutoResets,
                    aiResets = ai != null ? ai.ResetCount : 0, wallImpacts = wc != null ? wc.Impacts : 0,
                    worstImpact = wc != null ? wc.WorstSpeed : 0f, impactAt = wc != null ? wc.ImpactDistances.ToArray() : new float[0],
                    offTrackSeconds = off[p], maxLateral = maxLat[p], lapSeconds = lapsSec,
                    distance = p.Progress.Distance, speed = p.Car.SpeedKmh, nextGate = p.Progress.GatesPassedThisLap,
                    desiredSpeed = ai != null ? ai.LastDesiredSpeed : 0f, stuckSeconds = ai != null ? ai.StuckTime : 0f
                });
            }
            report.cars = cars.ToArray();
            string json = JsonUtility.ToJson(report, true);
            string outPath = Env("CG_OUT", "");
            if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, json);
            Debug.Log("[RaceHarness] " + json);
            Time.captureDeltaTime = 0f;
            Assert.Greater(report.ticks, 0);
        }

        private static void Apply(AiDriver ai, string parameters)
        {
            foreach (var kv in parameters.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = kv.Split('=');
                if (parts.Length != 2) continue;
                var f = typeof(AiDriver).GetField(parts[0]);
                Assert.IsNotNull(f, "AiDriver has no public field " + parts[0]);
                if (f.FieldType == typeof(bool)) f.SetValue(ai, bool.Parse(parts[1]));
                else f.SetValue(ai, float.Parse(parts[1], CultureInfo.InvariantCulture));
            }
        }
    }
}
