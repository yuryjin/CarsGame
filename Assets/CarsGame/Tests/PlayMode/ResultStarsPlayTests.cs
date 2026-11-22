using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class ResultStarsPlayTests
    {
        private string saved;
        private float capture;
        private GameFlow flow;
        private RaceEventDefinition practice;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        private static TMP_Text Label(string name, string parent = null) => System.Array.Find(
            Object.FindObjectsByType<TMP_Text>(), t => t.name == name && (parent == null || t.transform.parent.name == parent));

        [UnityTest, Timeout(180000)]
        public IEnumerator ResultsDistinguishCurrentSavedAndMissingStarsAndDoNotInventDailyStars()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.eventId = "qa_result_stars";
            practice.unlockAfterEventId = "";
            practice.aiCountOverride = 0; practice.pickupCount = 0;
            practice.skillGoal = SkillGoal.Kills; practice.star2TargetKills = 2;
            practice.finishGoal = FinishGoal.NoDeaths; practice.star3ZeroDeaths = true;
            var best = new EventBest { eventId = practice.eventId, bestTimeMs = 100000, stars = 4 };
            PlatformGlue.Save.Data.progress.bests.Add(best);

            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            race.Player.KillTally.Record(101);
            race.Player.Deaths = 1;
            race.QaFinishNow(); yield return null; yield return null;
            Assert.AreEqual(1, flow.LastStarsEarned);
            Assert.AreEqual(5, best.stars, "A missed survival objective must not erase the earlier achievement");
            Assert.AreEqual(Loc.Format("results.stars_this_race", "n", 1), Label("StarsTitle").text);
            Assert.AreEqual("★", Label("StarIcon", "Star0").text);
            Assert.AreEqual(Loc.Get("results.star_earned"), Label("StarStatus", "Star0").text);
            Assert.AreEqual("☆", Label("StarIcon", "Star1").text);
            Assert.AreEqual(Loc.Get("results.star_missed"), Label("StarStatus", "Star1").text);
            StringAssert.Contains(Loc.Format("results.goal.count", "n", 1, "target", 2), Label("StarDesc", "Star1").text);
            Assert.AreEqual("☆", Label("StarIcon", "Star2").text);
            Assert.AreEqual(Loc.Get("results.star_previous"), Label("StarStatus", "Star2").text);
            StringAssert.Contains(Loc.Format("results.goal.wrecks", "n", 1), Label("StarDesc", "Star2").text);
            Assert.AreNotEqual(Label("StarIcon", "Star0").color, Label("StarIcon", "Star2").color);

            flow.RetryRace();
            while (race.State == RaceState.Countdown) yield return null;
            race.Player.KillTally.Record(101); race.Player.KillTally.Record(102);
            race.QaFinishNow(); yield return null; yield return null;
            Assert.AreEqual(7, flow.LastStarsEarned);
            Assert.AreEqual(7, best.stars);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual("★", Label("StarIcon", "Star" + i).text);
                Assert.AreEqual(Loc.Get("results.star_earned"), Label("StarStatus", "Star" + i).text);
            }

            flow.RetryRace();
            while (race.State == RaceState.Countdown) yield return null;
            race.EndForPlayerOut(); yield return null; yield return null;
            Assert.IsFalse(race.Results.Player.Finished);
            Assert.AreEqual(Loc.Get("results.dnf"), Label("Place", "Medal").text);
            Assert.AreEqual(0, flow.LastStarsEarned);
            Assert.AreEqual(7, best.stars);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(Loc.Get("results.star_previous"), Label("StarStatus", "Star" + i).text);

            practice.finishGoal = FinishGoal.BeatTime;
            practice.targetSeconds = 2f;
            Time.captureDeltaTime = 0.125f;
            race.countdownSeconds = 0;
            foreach (bool late in new[] { false, true })
            {
                flow.RetryRace();
                while (race.State == RaceState.Countdown) yield return null;
                // A controlled race clock gives exactly the limit, then 125 ms over it.
                double target = practice.targetSeconds + (late ? 0.125 : 0);
                for (int i = 0; i < 20 && target > race.RaceTime; i++)
                    RaceClock.Advance((float)System.Math.Min(0.125, target - race.RaceTime));
                race.QaFinishNow(); yield return null;
                Assert.AreEqual(target, race.Results.Player.TotalTime);
                Assert.AreEqual(!late, (flow.LastStarsEarned & 4) != 0);
                string desc = StarGoalText.Description(practice, 2);
                if (late)
                {
                    string seconds = 0.125.ToString("0.000", System.Globalization.CultureInfo.GetCultureInfo(Loc.Language));
                    StringAssert.Contains(Loc.Format("results.goal.late", "t", seconds), Label("StarDesc", "Star2").text);
                    Assert.AreEqual(Loc.Get("results.star_previous"), Label("StarStatus", "Star2").text);
                }
                else Assert.AreEqual(desc, Label("StarDesc", "Star2").text);
            }

            flow.StartRace(flow.DailyEvent, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsNull(Label("Goals"), "Daily races do not award campaign stars");
            Assert.IsFalse(System.Array.Exists(Object.FindObjectsByType<UnityEngine.UI.Button>(), b => b.name == "LiveGoals"));
            race.QaFinishNow(); yield return null; yield return null;
            Assert.IsNull(Label("StarsTitle"), "The daily result cannot report three failed campaign objectives");
        }
    }
}
