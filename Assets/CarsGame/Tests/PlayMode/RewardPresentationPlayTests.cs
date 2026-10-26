using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class RewardPresentationPlayTests
    {
        private GameFlow flow;
        private string saved;
        private float capture;

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
            PauseController.ResetAll();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DailyRewardHeadlineMatchesTheActualWalletIncrease()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            PlatformGlue.Save.Data.progress.daily = new DailyData();
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            long before = PlatformGlue.Save.Data.Coins;
            flow.StartRace(flow.DailyEvent, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            race.Player.KillTally.Record(101); race.Player.KillTally.Record(101);
            race.Player.KillTally.Record(101); race.Player.KillTally.Record(102);
            race.QaFinishNow();
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.Greater(flow.LastDailyBonus, 0);
            long received = PlatformGlue.Save.Data.Coins - before;
            Assert.AreEqual(flow.LastReward + flow.LastDailyBonus, received);
            var reward = GameObject.Find("Summary").transform.Find("Reward").GetComponent<TMP_Text>();
            Assert.AreEqual(Loc.Format("results.reward", "n", received), reward.text,
                "The headline includes every coin paid by this result, including today's first-finish bonus");
            Assert.AreEqual(0, flow.LastFirstFinishBonus, "A first-place reward already exceeds the first-finish floor");
            Assert.Greater(flow.LastPayout.CombatCoins, 0);
            AssertBreakdown(received);

            until = Time.realtimeSinceStartup + 10f;
            while (PlatformGlue.InterstitialBusy && Time.realtimeSinceStartup < until) yield return null;
            flow.DoubleReward();
            until = Time.realtimeSinceStartup + 10f;
            while (!flow.LastRewardDoubled && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(flow.LastRewardDoubled);
            yield return new WaitForSecondsRealtime(1.1f);
            received = PlatformGlue.Save.Data.Coins - before;
            Assert.AreEqual(flow.LastReward * 2 + flow.LastDailyBonus, received, "The ad does not double the daily bonus");
            Assert.AreEqual(Loc.Format("results.reward", "n", received),
                GameObject.Find("Summary").transform.Find("Reward").GetComponent<TMP_Text>().text);
            AssertBreakdown(received);
            flow.DoubleReward();
            yield return null;
            Assert.AreEqual(received, PlatformGlue.Save.Data.Coins - before, "The same ad reward is credited once");

            before = PlatformGlue.Save.Data.Coins;
            flow.StartRace(flow.DailyEvent, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            Assert.IsFalse(flow.LastRewardDoubled);
            Assert.IsNull(GameObject.Find("RewardBreakdown"));
            race.QaFinishNow();
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.AreEqual(0, flow.LastDailyBonus, "Repeated daily finishes do not claim the bonus twice");
            received = PlatformGlue.Save.Data.Coins - before;
            Assert.AreEqual(flow.LastReward, received);
            AssertBreakdown(received);

            before = PlatformGlue.Save.Data.Coins;
            flow.StartRace(flow.DailyEvent, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            race.EndForPlayerOut();
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.AreEqual(0, flow.LastTotalReward, "A zero-lap DNF cannot reuse a previous payout");
            Assert.AreEqual(before, PlatformGlue.Save.Data.Coins);
            Assert.IsNull(GameObject.Find("Double"));
            AssertBreakdown(0);
        }

        private void AssertBreakdown(long expected)
        {
            if (GameObject.Find("RewardBreakdown") == null)
                GameObject.Find("RewardDetails").GetComponent<Button>().onClick.Invoke();
            var card = GameObject.Find("RewardBreakdown");
            Assert.NotNull(card);
            int sum = 0;
            foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                if (text.name.EndsWith("Coins") && text.name != "RewardTotalCoins")
                    sum += int.Parse(text.text.TrimStart('+'));
            Assert.AreEqual(expected, sum, "Visible components exactly explain the credited total");
            Assert.AreEqual("+" + expected, card.transform.Find("RewardTotalCoins").GetComponent<TMP_Text>().text);
            Assert.AreEqual(expected, flow.LastTotalReward);
        }
    }
}
