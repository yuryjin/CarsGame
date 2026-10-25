using System.Collections;
using NUnit.Framework;
using RacingGame.Core.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class AiCorneringPlayTests
    {
        private float capture;
        private string savedProfile, savedBody;
        private GameFlow flow;
        private RaceManager race;
        private RaceEventDefinition practice;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }

        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (race != null) race.spawner.playerBodyId = savedBody;
            if (savedProfile != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(savedProfile, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator HeavyBodiesFinishSpaceLapWithoutEdgeImpactsOrRecovery()
        {
            yield return Drive("e17", 1, new[] { "rhino", "hauler", "warden" });
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator LightAndHeavyBodiesSurviveTheFinalNarrowAbyssLap()
        {
            yield return Drive("e16", 3, new[] { "flea", "arrow", "hauler" });
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator GroundMergesDoNotLaunchCarsOffTheFlatMainRoad()
        {
            yield return Drive("e1", 1, new[] { "dune", "scorpion", "arrow", "grizzly" }, 0.2f);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ReverseGroundMergesDoNotLaunchCarsOffTheFlatMainRoad()
        {
            yield return Drive("e1", 1, new[] { "dune", "scorpion", "arrow", "grizzly" }, 0.2f, true);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator StarRingTunnelMouthsKeepFastBodiesOnTheFlatRoad()
        {
            yield return Drive("e19", 3, new[] { "sidewinder", "comet", "warden" }, 0.2f);
        }

        private IEnumerator Drive(string eventId, int laps, string[] bodies, float maximumHeight = float.PositiveInfinity, bool reverse = false)
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            savedProfile = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            savedBody = race.spawner.playerBodyId;
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == eventId));
            practice.unlockAfterEventId = "";
            practice.aiCountOverride = 0;
            practice.laps = laps;
            practice.reversed = reverse;
            var tier = System.Array.Find(flow.tiers, t => t.tierId == "normal");
            var bus = Object.FindAnyObjectByType<DebugBus>();
            foreach (string body in bodies)
            {
                var director = RaceDirector.Instance;
                director.raceSeed = 12345;
                typeof(RaceDirector).GetProperty("Rng").SetValue(director, new RngStreams(12345));
                race.spawner.playerBodyId = body;
                flow.StartRace(practice, tier);
                Assert.AreEqual(RaceState.Countdown, race.State, body);
                bus.Execute("autopilot", "on"); // Actual driving; no combat or progress injection.
                var player = race.Player;
                var starRing = Object.FindAnyObjectByType<StarRingTrack>();
                var impacts = player.gameObject.AddComponent<WallImpactCounter>();
                until = Time.realtimeSinceStartup + 30f;
                while (race.State == RaceState.Countdown && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(RaceState.Racing, race.State, body);
                int start = SimulationDriver.Instance.Tick;
                float peakHeight = 0f;
                while (!race.HasFinished(player) && SimulationDriver.Instance.Tick - start < 6000 * laps
                    && (race.State == RaceState.Racing || race.State == RaceState.Finishing))
                {
                    // Cars start half a metre above the grid and settle when physics starts.
                    // Ignore that deliberate spawn drop, well before the first merge.
                    bool AtTunnelExit(float exit)
                    {
                        float delta = player.Progress.Path.SignedDelta(exit, player.Progress.Distance);
                        return delta >= -5f && delta <= 30f;
                    }
                    bool flatSection = starRing == null || AtTunnelExit(starRing.Layout.TunnelEnd) || AtTunnelExit(starRing.Layout.RuinTunnelEnd);
                    if (SimulationDriver.Instance.Tick - start > 100 && flatSection)
                        peakHeight = Mathf.Max(peakHeight, player.transform.position.y - player.Progress.Path.Position(player.Progress.Distance).y);
                    yield return null;
                }
                Assert.IsTrue(race.HasFinished(player), body + " must finish within two simulated minutes per lap");
                Assert.LessOrEqual(peakHeight, maximumHeight, body + " must not jump on a flat road");
                Assert.AreEqual(laps, player.Progress.LapsCompleted, body + " must complete every track stage");
                Assert.AreEqual(0, player.Resets, body + " must stay on the road without recovery");
                Assert.AreEqual(0, impacts.Impacts, body + " must negotiate the outer shoulder without impact");
                Debug.Log($"[AiCornering] {eventId} {body} finished in {(SimulationDriver.Instance.Tick - start) * SimulationDriver.FixedStep:F2}s, no impacts or resets");
                flow.EnterMenu();
                yield return null; yield return null;
            }
        }
    }
}
