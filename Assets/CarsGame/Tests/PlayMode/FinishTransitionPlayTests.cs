using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class FinishTransitionPlayTests
    {
        private float capture;
        private string before;
        private GameFlow flow;
        private RaceEventDefinition practice;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (before != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(before, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }
        private static Button Button(string name) => System.Array.Find(Object.FindObjectsByType<Button>(), b => b.name == name);

        [UnityTest, Timeout(180000)]
        public IEnumerator ResultsDistinguishPermanentEliminationFromRespawningAndUnfinishedCars()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 2; practice.pickupCount = 0;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            RaceParticipant eliminated = null, respawning = null;
            foreach (var p in race.Participants)
            {
                if (p.isPlayer) continue;
                if (eliminated == null) eliminated = p; else respawning = p;
            }
            Assert.NotNull(eliminated); Assert.NotNull(respawning);
            eliminated.Health.respawnOnDeath = false;
            respawning.Health.respawnOnDeath = true;
            eliminated.Health.Detonate(null);
            respawning.Health.Detonate(null);
            race.EndForPlayerOut();
            yield return null;
            var outEntry = race.Results.Entries.Find(e => e.Name == eliminated.displayName);
            var waitingEntry = race.Results.Entries.Find(e => e.Name == respawning.displayName);
            Assert.IsTrue(outEntry.Eliminated);
            Assert.IsFalse(outEntry.Finished);
            Assert.IsFalse(waitingEntry.Eliminated, "A temporary wreck has not retired from the race");
            Assert.IsFalse(waitingEntry.Finished);
            Assert.IsFalse(race.Results.Player.Eliminated, "Healthy unfinished player is not eliminated");
            foreach (var entry in new[] { outEntry, waitingEntry })
            {
                var row = GameObject.Find("Result" + entry.Position);
                Assert.AreEqual(Loc.Get(entry.Eliminated ? "results.eliminated" : "results.dnf"),
                    row.transform.Find("Time").GetComponent<TMPro.TMP_Text>().text);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DnfAndTimedArenaCaptureCombatAtRaceEnd()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 0; practice.pickupCount = 0;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            race.Player.KillTally.Record(101); race.Player.KillTally.Record(101);
            race.EndForPlayerOut(); yield return null;
            Assert.IsFalse(race.Results.Player.Finished);
            Assert.IsFalse(race.Results.Player.Eliminated, "Ending a race is not proof that a car was eliminated");
            CollectionAssert.AreEqual(new[] { 2 }, race.Results.Player.KillsPerVictim);
            Assert.AreEqual(0, flow.LastReward, "Zero completed laps still cannot farm DNF coins");

            flow.EnterMenu();
            practice.mode = EventMode.Arena; practice.matchDuration = 1f;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            Assert.IsTrue(race.IsArenaMode);
            race.Player.KillTally.Record(101); race.Player.KillTally.Record(101); race.Player.KillTally.Record(202);
            race.Player.Deaths = 1;
            until = Time.realtimeSinceStartup + 5f;
            while (race.State == RaceState.Racing && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(RaceState.Finished, race.State, "Arena ends on its match clock");
            Assert.IsTrue(race.Results.Player.Finished);
            Assert.IsFalse(race.Results.Player.Survived);
            CollectionAssert.AreEqual(new[] { 2, 1 }, race.Results.Player.KillsPerVictim);
            Assert.AreEqual(3, race.Results.Player.Kills);
            var arenaRow = GameObject.Find("Result" + race.Results.Player.Position);
            Assert.AreEqual(Loc.Format("results.kills", "k", 3, "d", 1),
                arenaRow.transform.Find("Detail").GetComponent<TMPro.TMP_Text>().text,
                "Arena results show combat instead of a nonexistent best lap");
            int expected = RacingGame.Core.Economy.Economy.RaceReward(race.Results.Player.Position, true,
                new[] { 2, 1 }, RaceSetup.Tier.rewardMultiplier);
            Assert.AreEqual(expected, flow.LastReward);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NaturalFinishOffersResultsOnceAndFreezesThePlayerClock()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var touch = Object.FindAnyObjectByType<TouchControlsPresenter>();
            Assert.IsFalse(race.ShowResultsAfterFinish(), "Menus cannot grant a finish");
            PlatformGlue.Save.Data.bestGhost = "";
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.laps = 1; practice.aiCountOverride = 1; practice.pickupCount = 0;
            touch.forceShow = true;
            flow.StartRace(practice, flow.tiers[0]);
            Assert.IsFalse(race.ShowResultsAfterFinish(), "Countdown cannot be skipped to rewards");
            Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
            while (race.State == RaceState.Countdown) yield return null;
            foreach (var participant in race.Participants)
            {
                if (participant.isPlayer) continue;
                participant.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                var gunner = participant.GetComponent<AiGunner>();
                if (gunner != null) gunner.enabled = false;
            }
            Assert.IsFalse(race.ShowResultsAfterFinish(), "An unfinished player cannot skip the race");
            Assert.IsNull(Button("FinishResults"));
            // Inject deterministic score events around a real finish to test the payout boundary.
            race.Player.KillTally.Record(101);
            while (race.State == RaceState.Racing && race.RaceTime < 150d) yield return null;
            Assert.AreEqual(RaceState.Finishing, race.State, "The test driver must cross the real finish line");
            var savedGhost = RacingGame.Core.Ghost.GhostRecord.FromBase64(PlatformGlue.Save.Data.bestGhost);
            Assert.NotNull(savedGhost, "A natural finish saves a readable replay");
            Assert.AreEqual(race.PlayerElapsedTime, savedGhost.Duration, .002d,
                "The saved replay includes the final partial sample interval and the authoritative finish time");
            Assert.AreEqual(0, savedGhost.Samples[0].TimeMilliseconds, "The recording starts at GO");
            Assert.AreEqual(race.Player.GetComponent<CarBody>().BodyId, savedGhost.BodyId);
            yield return null;
            Assert.IsTrue(race.CanShowResults);
            Assert.NotNull(Button("FinishResults"));
            Assert.IsNull(Button("Fire"), "Driving controls must disappear after the finish");
            Assert.AreEqual(Button("FinishResults").gameObject, EventSystem.current.currentSelectedGameObject);
            double recorded = race.PlayerElapsedTime;
            int deaths = race.Player.Deaths;
            var timer = System.Array.Find(Object.FindObjectsByType<TMPro.TMP_Text>(), t => t.name == "Timer");
            string timeText = timer.GetParsedText();
            // The finish grace uses the real, pause-aware clock; simulation frames can run faster.
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.Greater(race.RaceTime, recorded + 2d, "The rest of the field can still race");
            Assert.AreEqual(recorded, race.PlayerElapsedTime);
            Assert.AreEqual(timeText, timer.GetParsedText(), "Displayed finish time must stop at the line");
            PauseController.Set(PauseReason.User, true); yield return null;
            Assert.IsNull(Button("FinishResults"));
            Assert.NotNull(Button("Resume"));
            PauseController.Set(PauseReason.User, false); yield return null;
            Assert.AreEqual(Button("FinishResults").gameObject, EventSystem.current.currentSelectedGameObject);
            // A collision after the line must not alter the recorded finish statistics.
            race.Player.Deaths++;
            race.Player.KillTally.Record(101);
            race.Player.KillTally.Record(202);
            long coins = PlatformGlue.Save.Data.Coins;
            int races = PlatformGlue.Save.Data.Races;
            Button("FinishResults").onClick.Invoke(); yield return null;
            Assert.AreEqual(FlowState.Results, flow.State);
            var unfinished = race.Results.Entries.Find(e => !e.IsPlayer);
            Assert.IsFalse(unfinished.Finished);
            Assert.IsFalse(unfinished.Eliminated, "The healthy rival was still driving when results were requested");
            Assert.AreEqual(0, unfinished.Laps);
            var row = GameObject.Find("Result" + unfinished.Position);
            Assert.NotNull(row);
            Assert.AreEqual(Loc.Get("results.dnf"), row.transform.Find("Time").GetComponent<TMPro.TMP_Text>().text);
            StringAssert.Contains(Loc.Format("results.laps_completed", "n", 0, "total", 1),
                row.transform.Find("Detail").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual(recorded, race.Results.Player.TotalTime);
            Assert.AreEqual(deaths, race.Results.Player.Deaths);
            int expectedReward = RacingGame.Core.Economy.Economy.RaceReward(race.Results.Player.Position, true,
                new[] { 1 }, RaceSetup.Tier != null ? RaceSetup.Tier.rewardMultiplier : 1f);
            Assert.AreEqual(expectedReward, flow.LastReward,
                $"Late kills cannot change the payout: recorded kills={race.Results.Player.Kills}, deaths={race.Results.Player.Deaths}, survived={race.Results.Player.Survived}");
            Assert.AreEqual(1, race.Results.Player.Kills, "Kills at the finish stay fixed");
            CollectionAssert.AreEqual(new[] { 1 }, race.Results.Player.KillsPerVictim);
            Assert.AreEqual(deaths == 0, race.Results.Player.Survived, "Survival must agree with the recorded deaths");
            Assert.AreEqual(races + 1, PlatformGlue.Save.Data.Races);
            Assert.AreEqual(coins + flow.LastReward, PlatformGlue.Save.Data.Coins);
            long paid = PlatformGlue.Save.Data.Coins;
            Assert.IsFalse(race.ShowResultsAfterFinish(), "Repeated result requests cannot pay twice");
            Assert.AreEqual(paid, PlatformGlue.Save.Data.Coins);
            flow.RetryRace(); yield return null;
            Assert.IsFalse(race.CanShowResults);
            Assert.IsNull(Button("FinishResults"), "Retry must hide the old finish card");
        }
    }
}
