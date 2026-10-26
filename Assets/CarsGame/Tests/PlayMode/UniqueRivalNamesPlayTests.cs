using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class UniqueRivalNamesPlayTests
    {
        private GameFlow flow;
        private RaceSpawner spawner;
        private RaceEventDefinition practice;
        private DifficultyTier tier;
        private string saved, language;
        private string[] originalNames;
        private AiDriverProfile[] originalProfiles;
        private float capture;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; language = Loc.Language; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (spawner != null) { spawner.aiNames = originalNames; spawner.profiles = originalProfiles; }
            if (saved != null && PlatformGlue.Save != null) { JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data); PlatformGlue.Save.MarkDirty(); }
            if (practice != null) Object.DestroyImmediate(practice);
            if (tier != null) Object.DestroyImmediate(tier);
            PauseController.ResetAll(); Time.captureDeltaTime = capture; Loc.SetLanguage(language);
        }

        private static List<string> Check(RaceManager race, string phase, List<string> failures)
        {
            var raw = new List<string>();
            var localized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in race.Participants)
            {
                if (p.isPlayer) continue;
                if (raw.Exists(n => string.Equals(n, p.displayName, StringComparison.OrdinalIgnoreCase))) failures.Add(phase + ": duplicate raw name " + p.displayName);
                raw.Add(p.displayName);
                if (!localized.Add(HudScreen.GetLocalizedRacerName(p.displayName))) failures.Add(phase + ": duplicate localized name " + p.displayName);
            }
            Debug.Log("[RivalNames] " + phase + ": " + string.Join(", ", raw));
            return raw;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RepeatedProfilesAndFallbackNamesStayDistinctAcrossRetries()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady); yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            Loc.SetLanguage("ru");
            until = Time.realtimeSinceStartup + 30f;
            while ((!Loc.Ready || Loc.Language != "ru") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(Loc.Ready);
            Assert.AreEqual("ru", Loc.Language);
            flow = Object.FindAnyObjectByType<GameFlow>();
            spawner = Object.FindAnyObjectByType<RaceSpawner>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            originalNames = spawner.aiNames; originalProfiles = spawner.profiles;
            practice = Object.Instantiate(Resources.Load<RaceEventDefinition>("Events/Event_e18"));
            practice.aiCountOverride = 5;
            tier = Object.Instantiate(Array.Find(flow.tiers, t => t.tierId == "easy"));
            var failures = new List<string>();
            flow.StartRace(practice, tier); yield return null;
            var first = Check(race, "actual Easy profile grid", failures);
            Assert.AreEqual(5, first.Count);
            int i = 0;
            foreach (var p in race.Participants) if (!p.isPlayer)
                Assert.AreSame(tier.profiles[i++ % tier.profiles.Length], p.GetComponent<AiDriver>().profile, "Naming must preserve driver profile order/frequency");
            flow.EnterMenu(); flow.StartRace(practice, tier); yield return null;
            CollectionAssert.AreEqual(first, Check(race, "retry", failures), "Retry must not accumulate numbering");
            Assert.AreEqual(spawner.playerName, race.Player.displayName);

            flow.EnterMenu(); practice.aiCountOverride = 7;
            tier.profiles = Array.Empty<AiDriverProfile>(); spawner.profiles = Array.Empty<AiDriverProfile>();
            spawner.aiNames = new[] { "Rex", "Rex", "Vega" };
            flow.StartRace(practice, tier); yield return null;
            Assert.AreEqual(7, Check(race, "repeated fallback names", failures).Count);
            flow.EnterMenu(); spawner.aiNames = new[] { "Bolt", "bolt" };
            flow.StartRace(practice, tier); yield return null;
            Assert.AreEqual(7, Check(race, "case-insensitive localized names", failures).Count);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
