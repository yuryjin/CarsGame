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
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CG_TRACE")))
                Debug.Log($"[WallImpact] {name} d={progress?.Distance:F1} hit={c.collider.name} parent={c.collider.transform.parent?.name} speed={v:F1} point={c.GetContact(0).point} normal={c.GetContact(0).normal}");
        }
    }

    /// <summary>
    /// Drives the real scene with AI in every seat and reports how the field raced: lap times (in
    /// simulation ticks), grass time, wall impacts, deaths and resets. One frame = one 50 Hz tick
    /// (<see cref="Time.captureDeltaTime"/>), so a race runs as fast as the CPU allows.
    /// Knobs for tuning runs come from environment variables:
    /// CG_EVENT (e1), CG_TIER (normal), CG_AI (rival count, -1 = event default), CG_LAPS,
    /// CG_SEED (12345), CG_PARAMS ("field=value ..." applied to every AiDriver), CG_OUT (JSON path),
    /// CG_BODY (player body id), CG_AI_BODIES (0 = every rival in the standard car).
    /// </summary>
    public sealed class RaceHarness
    {
        private const int TickLimit = 50 * 600;

        [Serializable] public class CarReport
        {
            public string name, body; public bool player, finished;
            public int laps, deaths, kills, autoResets, aiResets, wallImpacts, shortcuts, resets;
            public float offTrackSeconds, worstImpact, maxLateral, finishSeconds;
            public float distance, speed, desiredSpeed, stuckSeconds; public int nextGate;
            public float[] lapSeconds; public float[] impactAt;
        }
        [Serializable] public class Report
        {
            public string evt, tier, parameters; public int seed, ticks, shots, hits, mines, mineHits;
            public bool turretsEnabled, reversed;
            public int playerPosition, playerStarMask, playerSkillCount;
            public bool nextEventUnlocked;
            public float raceSeconds; public CarReport[] cars;
        }

        private float savedCaptureDelta;
        private string savedProfile;
        private GameFlow runningFlow;
        private RaceEventDefinition measuringEvent;
        [SetUp] public void SaveTimestep() { savedCaptureDelta = Time.captureDeltaTime; }
        [TearDown] public void RestoreTimestep()
        {
            if (runningFlow != null) runningFlow.EnterMenu();
            if (savedProfile != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(savedProfile, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (measuringEvent != null) UnityEngine.Object.DestroyImmediate(measuringEvent);
            Time.captureDeltaTime = savedCaptureDelta;
            LogAssert.ignoreFailingMessages = false;
        }

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
            Assert.IsTrue(PlatformGlue.IsReady, "platform never became ready");
            // A previous test can leave the persistent platform ready before this scene's
            // GameFlow.Start has run. Let startup enter the menu before spawning the field.
            yield return null; yield return null;
            savedProfile = JsonUtility.ToJson(PlatformGlue.Save.Data);

            var flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
            runningFlow = flow;
            var race = UnityEngine.Object.FindAnyObjectByType<RaceManager>();
            var bus = UnityEngine.Object.FindAnyObjectByType<DebugBus>();
            RaceEventDefinition ev = null; DifficultyTier tier = null;
            foreach (var e in flow.events) if (e != null && e.eventId == evtId) ev = e;
            foreach (var t in flow.tiers) if (t != null && t.tierId == tierId) tier = t;
            Assert.IsNotNull(ev, "event " + evtId);
            ev = UnityEngine.Object.Instantiate(ev);
            measuringEvent = ev;
            // This is a physical measuring fixture, not a progression journey. Only the clone
            // bypasses the campaign gate; the original event asset and saved unlocks stay intact.
            ev.unlockAfterEventId = "";
            if (rivals >= 0) ev.aiCountOverride = rivals;
            if (laps > 0) ev.laps = laps;
            string reverse = Env("CG_REVERSE", "");
            if (reverse.Length > 0) ev.reversed = reverse == "1";

            var director = RaceDirector.Instance;
            director.raceSeed = seed;
            typeof(RaceDirector).GetProperty("Rng").SetValue(director, new RngStreams(seed));
            race.spawner.playerBodyId = Env("CG_BODY", "");
            race.spawner.aiBodyVariety = Env("CG_AI_BODIES", "1") != "0";
            flow.StartRace(ev, tier);
            Assert.AreEqual(RaceState.Countdown, race.State, "The measuring race must actually start");
            bool turretsEnabled = Env("CG_TURRETS", "1") != "0";
            if (!turretsEnabled)
                foreach (var turret in UnityEngine.Object.FindObjectsByType<SpaceTurret>()) turret.enabled = false;
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

            // CG_TRACE="from to" (metres along the loop, may wrap), or "all": track geometry plus every car inside
            // the zone twice a second, written next to CG_OUT as *.trace.txt.
            string traceZone = Env("CG_TRACE", "");
            StringBuilder trace = null; float traceFrom = 0f, traceTo = 0f;
            var tracePath = TrackPath.Instance;
            if (traceZone.Length > 0 && tracePath != null)
            {
                if (traceZone == "all") { traceFrom = 0f; traceTo = tracePath.Length - 0.001f; }
                else
                {
                    var z = traceZone.Split(' ');
                    traceFrom = float.Parse(z[0], CultureInfo.InvariantCulture); traceTo = float.Parse(z[1], CultureInfo.InvariantCulture);
                }
                trace = new StringBuilder();
                trace.AppendLine($"# length={tracePath.Length:F1} halfWidth={tracePath.halfWidth:F2}");
                trace.AppendLine("# d  kappa(rad/m,+right)  profileKmh  lane  pos");
                float span = tracePath.SignedDelta(traceFrom, traceTo); if (span < 0f) span += tracePath.Length;
                for (float x = 0f; x <= span; x += 2f)
                {
                    float d = tracePath.Wrap(traceFrom + x);
                    Vector3 a = tracePath.Tangent(tracePath.Wrap(d - 3f)); a.y = 0f;
                    Vector3 b = tracePath.Tangent(tracePath.Wrap(d + 3f)); b.y = 0f;
                    float k = Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad / 6f;
                    Vector3 pos = tracePath.Position(d);
                    trace.AppendLine(string.Format(CultureInfo.InvariantCulture, "G {0,6:F1} {1,7:F4} {2,6:F1} {3,5:F2} ({4:F1},{5:F1})",
                        d, k, tracePath.Profile.SpeedAt(d) * 3.6f, tracePath.Profile.LaneAt(d), pos.x, pos.z));
                }
                trace.AppendLine("# tick car d lat(+right) kmh along thr steer brk stuck rev aiResets deaths yawDegS pathCurv(rad/m) x z surface desiredKmh y totalResets lane targetLane health wallRide gravityY staggered");
            }

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
                    if (trace != null && (tick - startTick) % 25 == 0)
                    {
                        float into = p.Progress.Path.SignedDelta(traceFrom, p.Progress.Distance);
                        float span = p.Progress.Path.SignedDelta(traceFrom, traceTo); if (span < 0f) span += p.Progress.Path.Length;
                        if (into < 0f) into += p.Progress.Path.Length;
                        if (into <= span)
                        {
                            var ai = p.GetComponent<AiDriver>(); var cmd = p.Car.LastCommand;
                            float signedLat = p.Progress.Path.Spline.LateralOffset(p.transform.position, p.Progress.Distance);
                            float yawDeg = p.Car.Body.angularVelocity.y * Mathf.Rad2Deg;
                            float speedMs = p.Car.Body.linearVelocity.magnitude;
                            trace.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                "C {0} {1} {2:F1} {3:F2} {4:F1} {5:F1} {6:F2} {7:F2} {8} {9:F2} {10} {11} {12} {13:F1} {14:F3} {15:F1} {16:F1} {17} {18:F1} {19:F2} {20} {21:F2} {22:F2} {23:F1} {24} {25:F3} {26}",
                                tick - startTick, p.name, p.Progress.Distance, signedLat, Mathf.Abs(p.Car.SpeedKmh), p.Progress.SpeedAlongTrack,
                                cmd.Throttle, cmd.Steer, cmd.Brake ? 1 : 0, ai != null ? ai.StuckTime : 0f, ai != null && ai.Reversing ? 1 : 0,
                                ai != null ? ai.ResetCount : 0, p.Deaths, yawDeg, speedMs > 1f ? yawDeg * Mathf.Deg2Rad / speedMs : 0f,
                                p.transform.position.x, p.transform.position.z, p.Car.Surface, ai != null ? ai.LastDesiredSpeed : 0f, p.transform.position.y, p.Resets,
                                ai != null ? ai.CurrentLane : 0f, ai != null ? ai.TargetLane : 0f,
                                p.Health.Health, p.Car.WallRiding ? 1 : 0, p.Car.GravityUp.y, p.Car.IsStaggered ? 1 : 0));
                        }
                    }
                }
                if (tick - startTick > TickLimit) break;
            }

            var report = new Report
            {
                evt = evtId, tier = tierId, parameters = parameters, seed = seed,
                ticks = SimulationDriver.Instance.Tick - startTick,
                shots = director.ShotsFired, hits = director.ShellHits,
                mines = director.MinesDropped, mineHits = director.MineHits, turretsEnabled = turretsEnabled, reversed = ev.reversed
            };
            report.raceSeconds = report.ticks * SimulationDriver.FixedStep;
            var finish = race.Results?.Player;
            if (finish != null)
            {
                report.playerPosition = finish.Position;
                report.playerStarMask = ev.StarMask(finish);
                report.playerSkillCount = ev.SkillCount(finish);
                var next = System.Array.Find(flow.events, candidate => candidate != null && candidate.unlockAfterEventId == ev.eventId);
                report.nextEventUnlocked = next != null && flow.IsUnlocked(next);
            }
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
                    name = p.name, body = p.GetComponent<CarBody>() != null ? p.GetComponent<CarBody>().BodyId : "", player = p.isPlayer, finished = finishTick.ContainsKey(p),
                    finishSeconds = finishTick.TryGetValue(p, out int ft) ? (ft - startTick) * SimulationDriver.FixedStep : -1f,
                    laps = p.Progress.LapsCompleted, deaths = p.Deaths, kills = p.Kills, autoResets = p.AutoResets, shortcuts = p.Shortcuts, resets = p.Resets,
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
            if (trace != null && !string.IsNullOrEmpty(outPath)) File.WriteAllText(Path.ChangeExtension(outPath, ".trace.txt"), trace.ToString());
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
