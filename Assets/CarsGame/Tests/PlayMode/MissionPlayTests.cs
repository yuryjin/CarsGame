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

namespace RacingGame.Tests
{
    /// <summary>
    /// The boss mission end to end in the real scene: the desert and the fortress appear, the field lines up in
    /// front of it, the escorts, outposts, drones, drops and raids happen, hits on units count, and the mission
    /// ends with a result and a results screen. The player's seat is driven by the mission bot.
    /// Knobs: CG_MISSION_OUT (log path), CG_MISSION_WIN=1 (hurt the core hard after a minute to reach the win),
    /// CG_MISSION_SHOTS=dir (render screenshots there; needs batch Unity without -nographics).
    /// </summary>
    public sealed class MissionPlayTests
    {
        private float savedCaptureDelta;
        private readonly List<string> errors = new List<string>();
        [SetUp] public void Save() { savedCaptureDelta = Time.captureDeltaTime; Application.logMessageReceived += OnLog; AudioListener.volume = 0f; }   // batch runs stay silent
        [TearDown] public void Restore() { Time.captureDeltaTime = savedCaptureDelta; Application.logMessageReceived -= OnLog; LogAssert.ignoreFailingMessages = false; AudioListener.volume = 1f; }
        private void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && errors.Count < 40) errors.Add(type + ": " + message + "\n" + stack);
        }

        private static string Env(string key, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator LeviathanMissionPlaysThrough()
        {
            LogAssert.ignoreFailingMessages = true;
            string outPath = Env("CG_MISSION_OUT", Path.Combine(Application.temporaryCachePath, "mission.txt"));
            string shots = Env("CG_MISSION_SHOTS", "");
            bool forceWin = Env("CG_MISSION_WIN", "0") == "1";
            var log = new StringBuilder();
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float wait = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady, "platform ready");
            yield return null;
            yield return null;

            var flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
            var race = UnityEngine.Object.FindAnyObjectByType<RaceManager>();
            var ev = Resources.Load<RaceEventDefinition>("Events/Event_m1");
            Assert.IsNotNull(ev, "mission event asset");
            Assert.AreEqual(EventMode.Mission, ev.mode);
            DifficultyTier tier = null;
            foreach (var t in flow.tiers) if (t != null && t.tierId == "normal") tier = t;
            var circuitGround = GameObject.Find("Ground_Terrain");
            flow.StartRace(ev, tier);

            var mission = MissionDirector.Current;
            Assert.IsNotNull(mission, "mission director");
            Assert.IsNotNull(mission.Map, "desert");
            Assert.IsNotNull(mission.Boss, "fortress");
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.AreEqual(6, race.Participants.Count, "player + five allies");
            if (circuitGround != null) Assert.IsFalse(circuitGround.activeSelf, "circuit hidden");
            foreach (var p in race.Participants)
            {
                Assert.AreEqual(Team.Players, Teams.Of(p.gameObject));
                Assert.IsTrue(mission.Map.Inside(p.transform.position), p.name + " starts inside the desert");
                if (!p.isPlayer) Assert.IsNotNull(p.GetComponent<MissionDriver>(), p.name + " uses the mission driver");
            }
            int unitsAtStart = MissionUnit.All.Count;
            Assert.Greater(unitsAtStart, 15, "boss parts, outposts");
            Assert.GreaterOrEqual(mission.AliveEscorts(), 5, "starting escort");

            // The player's seat is driven by the ally bot.
            var playerCar = race.Player.Car;
            var bot = playerCar.gameObject.AddComponent<MissionDriver>();
            bot.Initialize(mission, null, MissionDriver.Side.Ally, 5);
            if (playerCar.GetComponent<AiGunner>() == null) playerCar.gameObject.AddComponent<AiGunner>().Initialize(null, 0.8f);
            playerCar.SetInputSource(bot);

            while (race.State == RaceState.Countdown) yield return null;
            Assert.IsTrue(MissionUnit.Running, "units run after GO");
            int start = SimulationDriver.Instance.Tick;
            float bossStart = mission.Boss.Distance;
            int maxUnits = 0, maxDrones = 0, maxTroopers = 0, raids = 0, maxStrikes = 0;
            mission.PlaneRaid += () => raids++;
            var announced = new List<string>();
            mission.Announce += (text, color) => announced.Add(text);
            // Who hurts the team, and what the team hurts: the numbers balance is tuned by.
            var taken = new Dictionary<string, float>();
            var dealt = new Dictionary<string, float>();
            void Add(Dictionary<string, float> d, string k, float v) { d.TryGetValue(k, out float o); d[k] = o + v; }
            foreach (var p in race.Participants)
                p.Health.HitResolved += (hit, result) => { if (result.DamageDealt > 0f) Add(taken, Source(hit.Attacker) + "/" + hit.HitType, result.DamageDealt); };
            Action<MissionUnit, GameObject, float> onDamaged = (u, a, d) => Add(dealt, (u is BossPart bp ? "boss." + bp.Role : u.Kind.ToString()) + (a == playerCar.gameObject ? " (player)" : ""), d);
            MissionUnit.Damaged += onDamaged;
            int shotIndex = 0;
            float coreAtMinute = -1f;
            while (race.State == RaceState.Racing || race.State == RaceState.Finishing)
            {
                yield return null;
                int tick = SimulationDriver.Instance.Tick - start;
                maxUnits = Mathf.Max(maxUnits, MissionUnit.All.Count);
                maxDrones = Mathf.Max(maxDrones, mission.Count(UnitKind.Drone));
                maxTroopers = Mathf.Max(maxTroopers, mission.Count(UnitKind.Infantry) + mission.Count(UnitKind.RocketTrooper));
                maxStrikes = Mathf.Max(maxStrikes, mission.Strikes.Count);
                if (tick % 500 == 0) log.AppendLine(Line(tick, mission, race));
                if (tick == 3000) coreAtMinute = mission.Boss.CoreFraction;
                if (forceWin && tick >= 3000 && tick % 50 == 0 && mission.Boss.Core.Alive)
                    mission.Boss.Core.TakeHit(playerCar.gameObject, 400f, mission.Boss.Core.transform.position);
                if (shots.Length > 0 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null
                    && (tick == 150 || tick == 1500 || tick == 4000 || tick == 7000 || tick == 10000))
                {
                    Capture(Camera.main, Path.Combine(shots, $"chase_{shotIndex}.png"));
                    CaptureOverview(mission, Path.Combine(shots, $"overview_{shotIndex}.png"));
                    shotIndex++;
                }
                if (tick > 50 * 480) break;
            }
            log.AppendLine(Line(SimulationDriver.Instance.Tick - start, mission, race));
            log.AppendLine($"state={race.State} result={mission.Result} victory={MissionDirector.LastVictory} bossMoved={mission.Boss.Distance - bossStart:F0}m coreAtMinute={coreAtMinute:F2}");
            log.AppendLine($"maxUnits={maxUnits} maxDrones={maxDrones} maxTroopers={maxTroopers} raids={raids} maxStrikes={maxStrikes} shots={RaceDirector.Current.ShotsFired} hits={RaceDirector.Current.ShellHits}");
            log.AppendLine("reward=" + flow.LastReward + " stars=" + flow.LastStarsEarned);
            if (race.Results != null)
                foreach (var e in race.Results.Entries) log.AppendLine($"  #{e.Position} {e.Name}{(e.IsPlayer ? "*" : "")} fin={e.Finished} K={e.Kills} D={e.Deaths}");
            MissionUnit.Damaged -= onDamaged;
            log.AppendLine("taken: " + Dump(taken));
            log.AppendLine("dealt to units: " + Dump(dealt));
            var parts = new StringBuilder();
            foreach (var bp in mission.Boss.Parts) parts.Append(bp.Role).Append(bp.Alive ? "+ " : "- ");
            log.AppendLine("boss parts: " + parts);
            log.AppendLine("announcements: " + string.Join(" | ", announced));
            log.AppendLine("errors: " + errors.Count);
            foreach (var e in errors) log.AppendLine(e);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, log.ToString());
            Debug.Log("[MissionPlayTests]\n" + log);

            Assert.AreEqual(0, errors.Count, "no errors or exceptions: " + (errors.Count > 0 ? errors[0] : ""));
            Assert.Greater(mission.Boss.Distance - bossStart, 100f, "the fortress crawls");
            Assert.Greater(RaceDirector.Current.ShellHits, 20, "shells land");
            Assert.Greater(raids, 0, "a bomber raid happened");
            if (forceWin)
            {
                Assert.AreEqual(RaceState.Finished, race.State, "mission ended");
                Assert.IsTrue(MissionDirector.LastVictory, "core destroyed = victory");
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.Greater(flow.LastReward, 0);
            }

            // Back to the menu gives the circuit back and leaves nothing of the mission behind.
            flow.EnterMenu();
            yield return null;
            yield return null;
            Assert.IsNull(MissionDirector.Current);
            Assert.AreEqual(0, MissionUnit.All.Count);
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<DesertMap>());
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<BossCrawler>());
            if (circuitGround != null) Assert.IsTrue(circuitGround.activeSelf, "circuit restored");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("Car"), MissionLayers.SoftUnit));
        }

        private static string Source(GameObject attacker)
        {
            if (attacker == null) return "world";
            var unit = attacker.GetComponent<MissionUnit>();
            if (unit is BossPart part) return part.Role == BossRole.Hull ? "boss" : "boss." + part.Role;
            if (unit != null) return unit.Kind.ToString();
            if (attacker.GetComponent<BossCrawler>() != null) return "boss";
            return Teams.Of(attacker) == Team.Enemy ? "escort:" + attacker.name.Split('_')[1] : "ally:" + attacker.name;
        }

        private static string Dump(Dictionary<string, float> d)
        {
            var keys = new List<string>(d.Keys);
            keys.Sort((a, b) => d[b].CompareTo(d[a]));
            var sb = new StringBuilder();
            foreach (var k in keys) sb.Append(k).Append('=').Append(d[k].ToString("F0", CultureInfo.InvariantCulture)).Append("  ");
            return sb.ToString();
        }

        private static string Line(int tick, MissionDirector m, RaceManager race)
        {
            var p = race.Player;
            return string.Format(CultureInfo.InvariantCulture,
                "t={0,5:F0}s boss={1,5:F0}m core={2:F2} ph={3} esc={4} drones={5} troops={6} turrets={7} planes={8} strikes={9} player=({10:F0},{11:F0}) kmh={12:F0} K={13} D={14}",
                tick * SimulationDriver.FixedStep, m.Boss.Distance, m.Boss.CoreFraction, m.Boss.Phase, m.AliveEscorts(), m.Count(UnitKind.Drone),
                m.Count(UnitKind.Infantry) + m.Count(UnitKind.RocketTrooper), m.Count(UnitKind.Turret), m.Count(UnitKind.Plane), m.Strikes.Count,
                p.transform.position.x - DesertMap.Origin.x, p.transform.position.z - DesertMap.Origin.z, p.Car.SpeedKmh, p.Kills, p.Deaths);
        }

        private static void Capture(Camera cam, string file)
        {
            if (cam == null) return;
            var rt = new RenderTexture(1280, 720, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
        }

        // A second camera off the fortress's front quarter, showing it, its wheel and the fight around it.
        private static void CaptureOverview(MissionDirector m, string file)
        {
            var main = Camera.main;
            var go = new GameObject("OverviewCam");
            var cam = go.AddComponent<Camera>();
            if (main != null) { cam.CopyFrom(main); cam.targetTexture = null; }
            cam.farClipPlane = 1500f;
            var boss = m.Boss.transform;
            go.transform.position = boss.position + boss.forward * 70f + boss.right * 55f + Vector3.up * 30f;
            go.transform.LookAt(boss.position + Vector3.up * 10f);
            Capture(cam, file);
            UnityEngine.Object.Destroy(go);
        }
    }
}
