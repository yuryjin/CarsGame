using System;
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using RacingGame.Core.Save;
using RacingGame.Platform;
using RacingGame.Platform.Mock;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class AccountDifficultyPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator SigningInRestoresTheCloudDifficultyOnTheOpenCardAndItsFinishReward()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var glue = flow.GetComponent<PlatformGlue>();
            var menu = flow.GetComponent<MenuScreens>();
            var race = flow.GetComponent<RaceManager>();
            var save = PlatformGlue.Save;
            string profile = SaveMigrator.ToJson(save.Data);
            var previousPlatform = PlatformRuntime.Current;
            bool wasAuthorized = EditorMockPlatform.Authorized;
            float capture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep * 2;
                EditorMockPlatform.Authorized = false;
                var platform = new EditorMockPlatform();
                var initialized = platform.InitializeAsync();
                while (!initialized.IsCompleted) yield return null;
                Assert.IsTrue(initialized.Result.Ok);
                // Subscribe the live platform glue to this guest, including its actual identity-change event.
                glue.enabled = false;
                PlatformRuntime.Install(platform);
                glue.enabled = true;
                var tiers = Array.FindAll(flow.tiers, t => t != null);
                var easy = Array.Find(tiers, t => t.tierId == "easy");
                int hard = Array.FindIndex(tiers, t => t.tierId == "hard");
                menu.Show("events");
                Press("Tier" + hard);
                var flushed = save.FlushAsync();
                while (!flushed.IsCompleted) yield return null;
                Assert.IsTrue(flushed.Result.Ok);
                Assert.AreEqual("hard", save.Data.settings.tier);

                var remote = SaveMigrator.FromJson(SaveMigrator.ToJson(save.Data));
                remote.settings.tier = "easy";
                remote.updatedAtUnixMs += 1000;
                platform.CloudSave.Queue(SaveMigrator.ToJson(remote));
                var staged = platform.CloudSave.FlushAsync();
                while (!staged.IsCompleted) yield return null;
                Assert.IsTrue(staged.Result.Ok);
                menu.Show("leaders");
                Press("SignIn");
                menu.Show("events");  // The card is already visible while the account is loading.
                deadline = Time.realtimeSinceStartup + 20f;
                while ((!save.CloudSynced || save.Data.settings.tier != "easy") && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(platform.Player.IsAuthorized);
                Assert.AreEqual("easy", save.Data.settings.tier);
                yield return null;
                var start = GameObject.Find("Start_tt1").GetComponent<Button>();
                string displayed = start.transform.parent.Find("Difficulty").GetComponent<TMP_Text>().text;
                string multiplier = easy.rewardMultiplier.ToString("0.##", CultureInfo.GetCultureInfo(Loc.Language));
                StringAssert.Contains(Loc.Format("events.race_reward", "m", multiplier), displayed);

                long balance = save.Data.Coins;
                start.onClick.Invoke();
                Assert.AreSame(easy, RaceSetup.Tier);
                while (race.State == RaceState.Countdown) yield return null;
                Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
                while (race.State == RaceState.Racing && race.RaceTime < 240f) yield return null;
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.IsTrue(race.Results.Player.Finished);
                Assert.AreEqual(easy.rewardMultiplier, flow.LastPayout.Multiplier);
                Assert.Greater(flow.LastTotalReward, 0);
                Assert.AreEqual(balance + flow.LastTotalReward, save.Data.Coins);
                Assert.AreEqual("easy", save.Data.settings.tier);
            }
            finally
            {
                flow.EnterMenu();
                glue.enabled = false;
                PlatformRuntime.Install(previousPlatform);
                EditorMockPlatform.Authorized = wasAuthorized;
                JsonUtility.FromJsonOverwrite(profile, save.Data);
                save.MarkDirty();
                glue.enabled = true;
                Time.captureDeltaTime = capture;
            }
        }

        private static void Press(string name) => GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
    }
}
