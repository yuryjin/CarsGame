using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class RecordSetupPlayTests
    {
        private string saved;
        private float capture;
        private int trial;
        private GameFlow flow;
        private RaceManager race;
        private RaceEventDefinition practice;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; trial = RewardedOffers.TrialUpgrade; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            RewardedOffers.GrantTrial((UpgradeKind)trial);
            Time.captureDeltaTime = capture;
        }

        private IEnumerator Finish(double seconds)
        {
            while (race.State == RaceState.Countdown) yield return null;
            for (int i = 0; i < 200 && race.RaceTime < seconds; i++) RaceClock.Advance(0.25f);
            Assert.GreaterOrEqual(race.RaceTime, seconds, "Fixture clock must be advancing");
            race.QaFinishNow(); yield return null;
            Assert.AreEqual(FlowState.Results, flow.State);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RecordsCaptureAppliedTrialAndCompareOnlyMatchingConfigurations()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 0; practice.pickupCount = 0; practice.laps = 1;
            var p = PlatformGlue.Save.Data.progress;
            p.bests.RemoveAll(b => b.eventId == "e1");
            p.upgrades = new int[3];
            p.bests.Add(new EventBest { eventId = "e1", bestTimeMs = 1, stars = 1 });
            RewardedOffers.ConsumeTrial(null);
            flow.StartRace(practice, flow.tiers[0]);
            yield return Finish(5d);
            Assert.IsTrue(flow.LastHadPreviousBest);
            Assert.IsFalse(flow.LastBestSetupKnown);
            Assert.IsFalse(flow.LastBestComparable);
            Assert.AreEqual(Loc.Get("results.pb_unknown_setup"),
                GameObject.Find("Record").GetComponent<TMPro.TMP_Text>().text);

            flow.EnterMenu();
            p.bests.RemoveAll(b => b.eventId == "e1");
            RewardedOffers.GrantTrial(UpgradeKind.Engine);
            flow.StartRace(practice, flow.tiers[0]);
            yield return Finish(5d);
            var best = p.bests.Find(b => b.eventId == "e1");
            Assert.IsTrue(flow.LastWasPersonalBest);
            Assert.AreEqual(1, best.setup.upgrades[0], "Record describes the upgrade actually used in this race");
            Assert.AreEqual(0, p.upgrades[0], "Trial does not become a permanent purchase");
            Assert.AreEqual(race.Player.GetComponent<CarBody>().BodyId, best.setup.body);
            Assert.AreEqual(RaceSetup.TierId, best.setup.tier);
            Assert.AreEqual(1, best.setup.laps);
            long recorded = best.bestTimeMs;

            flow.RetryRace();
            yield return Finish(10d);
            Assert.IsTrue(flow.LastBestSetupKnown);
            Assert.IsFalse(flow.LastBestComparable, "Without the trial, the same body is still a different setup");
            Assert.AreEqual(recorded, best.bestTimeMs);
            Assert.AreEqual(1, best.setup.upgrades[0]);
            Assert.AreEqual(Loc.Get("results.pb_other_setup"), GameObject.Find("Record").GetComponent<TMPro.TMP_Text>().text);

            flow.EnterMenu(); p.upgrades[0] = 1;
            flow.StartRace(practice, flow.tiers[0]);
            yield return Finish(10d);
            Assert.IsTrue(flow.LastBestComparable, "Permanent and trial levels with identical effects are comparable");
            Assert.Greater(flow.LastBestDeltaMs, 0);
            Assert.AreEqual(recorded, best.bestTimeMs);
            Assert.IsTrue(RecordSetup.Same(best.setup, SaveMigrator.FromJson(SaveMigrator.ToJson(PlatformGlue.Save.Data)).progress.bests.Find(b => b.eventId == "e1").setup));
        }
    }
}
