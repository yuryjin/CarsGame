using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Save;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class SaveRecoveryPlayTests
    {
        private sealed class FailingStore : ILocalSaveStore
        {
            public bool Fail, FailRead;
            public string Json;
            public string Read() => FailRead ? throw new System.IO.IOException("QA read denied") : Json;
            public void Write(string json)
            {
                if (Fail) throw new System.IO.IOException("QA quota full");
                Json = json;
            }
        }
        private SaveService original;
        private float capture, originalEffects;
        private string language;
        private static void SetSave(SaveService save) => typeof(PlatformGlue).GetProperty("Save").SetValue(null, save);
        private static Button Button(string name) => System.Array.Find(Object.FindObjectsByType<Button>(), b => b.name == name);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (original != null)
            {
                SetSave(original);
                SfxPlayer.Instance.volume = originalEffects;
                PlatformGlue.ApplyQuality();
                Loc.SetLanguage(language);
                float until = Time.realtimeSinceStartup + 30f;
                while (Loc.Language != language && Time.realtimeSinceStartup < until) yield return null;
            }
            Object.FindAnyObjectByType<GameFlow>()?.EnterMenu();
            PauseController.ResetAll(); Time.captureDeltaTime = capture;
            yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ReadRecoveryRefreshesTheWalletAndRestoresMutedEffects()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            original = PlatformGlue.Save; language = Loc.Language; originalEffects = SfxPlayer.Instance.volume;
            var profile = new SaveData { updatedAtUnixMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            profile.settings.lang = language; profile.settings.sfx = 0f;
            profile.Local("existing-device").coinsEarned = 321;
            var store = new FailingStore { FailRead = true, Json = SaveMigrator.ToJson(profile) };
            var save = new SaveService(store, () => null, () => Time.realtimeSinceStartup, "current-device");
            var loading = save.LoadAsync(); while (!loading.IsCompleted) yield return null;
            SetSave(save);
            var menus = Object.FindAnyObjectByType<MenuScreens>(); menus.Show("main");
            yield return null; yield return null;
            Assert.IsTrue(save.LocalReadFailed);
            Assert.NotNull(Button("SaveRetry"));
            store.FailRead = false; Button("SaveRetry").onClick.Invoke();
            yield return null; yield return null;
            Assert.IsFalse(save.LocalReadFailed);
            Assert.AreEqual(321, save.Data.Coins);
            var wallet = GameObject.Find("Coins").GetComponent<TMPro.TMP_Text>();
            StringAssert.Contains("321", wallet.text, "Recovered coins must be visible immediately without reopening the menu");
            Assert.AreEqual(0f, SfxPlayer.Instance.volume, "A recovered mute setting must affect the actual sound output");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FailedSavingExplainsRecoveryWithoutInterruptingDrivingOrClearingUserPause()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            original = PlatformGlue.Save; language = Loc.Language; originalEffects = SfxPlayer.Instance.volume;
            var store = new FailingStore { Fail = true };
            var save = new SaveService(store, () => null, () => Time.realtimeSinceStartup, "save-ui");
            var loading = save.LoadAsync(); while (!loading.IsCompleted) yield return null;
            SetSave(save);
            var menus = Object.FindAnyObjectByType<MenuScreens>();
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            menus.Show("main");
            save.Local.coinsEarned = 123; save.MarkDirty();
            yield return null; yield return null;
            foreach (string code in new[] { "en", "ru", "tr", "zh", "de", "es", "fr", "pt", "uk" })
            {
                PlatformGlue.SetLanguageOverride(code);
                until = Time.realtimeSinceStartup + 30f;
                while ((Loc.Language != code || PlatformGlue.Save.Data.settings.lang != code) && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(code, Loc.Language);
                yield return null; yield return null;
                Assert.NotNull(Button("SaveRetry")); Assert.NotNull(Button("SaveContinue"));
                var message = GameObject.Find("SaveRecoveryMessage").GetComponent<TMPro.TMP_Text>();
                message.ForceMeshUpdate();
                Assert.AreEqual(Loc.Get("save.local.message"), message.text);
                Assert.LessOrEqual(message.GetPreferredValues(message.text, message.rectTransform.rect.width, float.PositiveInfinity).y,
                    message.rectTransform.rect.height + 1f, code + " message must fit");
            }
            Button("SaveContinue").onClick.Invoke(); yield return null;
            Assert.IsNull(Button("SaveRetry"));
            menus.Show("settings"); yield return null;
            Assert.NotNull(Button("RetryLocalSave"));
            Button("RetryLocalSave").onClick.Invoke(); yield return null;
            Assert.NotNull(Button("SaveRetry"));
            Button("SaveContinue").onClick.Invoke();
            store.Fail = false; Assert.IsTrue(save.RetryLocalWrite()); yield return null;
            flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            store.Fail = true; save.MarkDirty();
            double before = RaceClock.Now;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsNull(Button("SaveRetry"), "Saving must not cover active driving with a modal");
            Assert.Greater(RaceClock.Now, before);
            PauseController.Set(PauseReason.User, true); yield return null; yield return null;
            Assert.NotNull(Button("SaveRetry"));
            Button("SaveRetry").onClick.Invoke(); yield return null;
            Assert.IsTrue(save.LocalWriteFailed);
            store.Fail = false; Button("SaveRetry").onClick.Invoke(); yield return null;
            Assert.IsFalse(save.LocalWriteFailed);
            Assert.IsNull(Button("SaveRetry"));
            Assert.AreEqual(123, SaveMigrator.FromJson(store.Json).Coins);
            Assert.IsTrue((PauseController.Reasons & PauseReason.User) != 0, "Successful saving must not resume the user's paused race");
        }
    }
}
