using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Daily;

namespace RacingGame.Tests
{
    public sealed class RacerNamesPlayTests
    {
        private float capture;
        private string before, language;
        private GameFlow flow;
        private RaceEventDefinition practice;
        private readonly List<string> missingNames = new List<string>();
        [SetUp] public void Setup()
        {
            capture = Time.captureDeltaTime;
            language = Loc.Language;
            Application.logMessageReceived += CaptureMissingName;
        }
        [TearDown] public void Cleanup()
        {
            Application.logMessageReceived -= CaptureMissingName;
            if (flow != null) flow.EnterMenu();
            if (before != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(before, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
            Loc.SetLanguage(language);
        }
        private void CaptureMissingName(string message, string stack, LogType type)
        {
            if (message.StartsWith("[Loc] missing key: ai.name.")) missingNames.Add(message);
        }
        private static void Expect(List<string> failures, string expected, string actual, string label)
        {
            if (actual != expected) failures.Add(label + ": expected '" + expected + "', got '" + actual + "'");
        }
        private static void Park(RaceManager race)
        {
            int slot = 0;
            foreach (var p in race.Participants)
            {
                p.Car.inputEnabled = false;
                p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                TrackPath.Instance.Pose(50f + slot++ * 100f, 0f, out var pos, out var rot);
                p.Car.Teleport(pos + Vector3.up * 0.5f, rot);
            }
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator LiteralAndLocalizedNamesStayConsistentAcrossCombatSurfaces()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            Loc.SetLanguage("ru");
            until = Time.realtimeSinceStartup + 30f;
            while (!Loc.Ready && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(Loc.Ready);
            var failures = new List<string>();
            Expect(failures, Loc.Get("ai.name.vega") + " 2", HudScreen.GetLocalizedRacerName("Vega 2"), "numbered rival");
            Expect(failures, "Guest 42", HudScreen.GetLocalizedRacerName("Guest 42"), "literal guest name");
            string armored = Loc.Get("mission.unit.armored");
            Expect(failures, armored, HudScreen.GetLocalizedRacerName(armored), "already localized mission unit");
            Expect(failures, Loc.Get("player.name"), HudScreen.GetLocalizedRacerName("Guest 42", true), "player name");

            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var hud = Object.FindAnyObjectByType<HudScreen>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 1; practice.pickupCount = 0;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            Park(race);
            RaceParticipant rival = null;
            foreach (var p in race.Participants) if (!p.isPlayer) rival = p;
            Assert.NotNull(rival);
            rival.displayName = "Vega 2";
            // Controlled fatal damage exercises the actual score/death/notification event chain.
            // It is not a claim that these kills were made through browser combat inputs.
            race.Player.Health.Detonate(rival.gameObject);
            Expect(failures, Loc.Format("hud.killed_by", "name", Loc.Get("ai.name.vega") + " 2"), hud.LastEvent, "death toast");
            race.Player.Health.ReviveNow();
            rival.Health.Detonate(race.Player.gameObject);
            Expect(failures, Loc.Format("hud.kill", "name", Loc.Get("ai.name.vega") + " 2"), hud.LastEvent, "kill toast");
            rival.Health.ReviveNow();
            rival.displayName = "<b>Guest</b>";
            race.Player.Health.Detonate(rival.gameObject);
            Expect(failures, Loc.Format("hud.killed_by", "name", UiFactory.Escape(rival.displayName)), hud.LastEvent, "literal rich-text-like name");

            flow.EnterMenu(); practice.twist = RaceTwist.HotBomb;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            Park(race);
            foreach (var p in race.Participants) if (!p.isPlayer) rival = p;
            rival.displayName = "Vega 2";
            var bomb = race.Player.HotBomb;
            for (int i = 0; i < 85; i++) yield return null;
            Assert.AreSame(race.Player, bomb.Carrier);
            Assert.IsTrue(bomb.Touch(race.Player, rival));
            yield return null; yield return null;
            string bombText = Object.FindAnyObjectByType<RaceOverlays>().BombMessage;
            if (!bombText.Contains(Loc.Get("ai.name.vega") + " 2")) failures.Add("bomb owner: " + bombText);
            if (missingNames.Count > 0) failures.AddRange(missingNames);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
