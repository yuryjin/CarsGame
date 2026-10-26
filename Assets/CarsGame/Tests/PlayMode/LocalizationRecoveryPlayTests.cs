using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;

namespace RacingGame.Tests
{
    public sealed class LocalizationRecoveryPlayTests
    {
        private sealed class RecoveringTable : ITableProvider
        {
            public bool Fail = true;
            public StringTable Replacement;
            public int Attempts;
            public AsyncOperationHandle<TTable> ProvideTableAsync<TTable>(string name, Locale locale) where TTable : LocalizationTable
            {
                if (locale.Identifier.Code != "it") return default;
                Attempts++;
                // A missing Addressables table returns null. Once the simulated server recovers,
                // this provider supplies the same collection in the requested locale.
                return Fail ? default : Addressables.ResourceManager.CreateCompletedOperation(Replacement as TTable, null);
            }
        }

        private ITableProvider previousProvider;
        private string previousOverride;
        private float capture;
        private bool ignore;
        private bool booted;
        private Locale addedLocale;
        private StringTable replacement;
        private int failures;
        private void OnLoadFailed(string code) { if (code == "it") failures++; }
        private static IEnumerator WaitFor(System.Func<bool> complete, string message)
        {
            float until = Time.realtimeSinceStartup + 30f;
            while (!complete() && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(complete(), message);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Loc.LoadFailed -= OnLoadFailed;
            if (booted)
            {
                LocalizationSettings.StringDatabase.TableProvider = previousProvider;
                PlatformGlue.Save.Data.settings.lang = previousOverride;
                bool restored = false;
                Loc.Initialize("en", previousOverride, loaded => restored = loaded);
                float until = Time.realtimeSinceStartup + 30f;
                while (!restored && Time.realtimeSinceStartup < until) yield return null;
                Object.FindAnyObjectByType<MenuScreens>()?.Show("main");
                if (addedLocale != null) LocalizationSettings.AvailableLocales.RemoveLocale(addedLocale);
                if (addedLocale != null) Object.DestroyImmediate(addedLocale);
                if (replacement != null) Object.DestroyImmediate(replacement);
            }
            LogAssert.ignoreFailingMessages = ignore;
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator InterruptedLanguageSwitchKeepsWorkingMenusAndCanRetry()
        {
            capture = Time.captureDeltaTime; ignore = LogAssert.ignoreFailingMessages;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            booted = true;
            previousOverride = PlatformGlue.Save.Data.settings.lang;
            previousProvider = LocalizationSettings.StringDatabase.TableProvider;
            PlatformGlue.SetLanguageOverride("en");
            yield return WaitFor(() => Loc.Ready && Loc.Language == "en" && PlatformGlue.Save.Data.settings.lang == "en", "English setup completes");
            Assert.AreEqual("en", Loc.Language);
            string title = Loc.Get("menu.title");
            addedLocale = Locale.CreateLocale("it");
            LocalizationSettings.AvailableLocales.AddLocale(addedLocale);
            replacement = Object.Instantiate(LocalizationSettings.StringDatabase.GetTableAsync(Loc.Table).Result);
            replacement.LocaleIdentifier = addedLocale.Identifier;
            replacement.AddEntry("menu.title", "Recovered locale");
            var outage = new RecoveringTable { Replacement = replacement };
            LocalizationSettings.StringDatabase.TableProvider = outage;
            LogAssert.ignoreFailingMessages = true; // The resource manager reports this deliberately failed dependency too.
            failures = 0;
            Loc.LoadFailed += OnLoadFailed;
            PlatformGlue.SetLanguageOverride("it");
            yield return WaitFor(() => failures > 0, "The deliberately missing table must finish with a failure");
            Assert.Greater(outage.Attempts, 0);
            Assert.IsTrue(Loc.Ready, "A failed download must retain the previous usable translation");
            Assert.AreEqual("en", Loc.Language);
            Assert.AreEqual("en", PlatformGlue.Save.Data.settings.lang, "Only a successfully loaded choice is saved");
            Assert.AreEqual(title, Loc.Get("menu.title"));
            Assert.AreEqual("en", LocalizationSettings.SelectedLocale.Identifier.Code);
            var menus = Object.FindAnyObjectByType<MenuScreens>();
            menus.Show("settings"); yield return null;
            Assert.NotNull(GameObject.Find("Panel_settings"));
            menus.Show("main"); yield return null;
            Assert.NotNull(GameObject.Find("Panel_main"));
            outage.Fail = false;
            LogAssert.ignoreFailingMessages = ignore;
            PlatformGlue.SetLanguageOverride("it");
            yield return WaitFor(() => Loc.Language == "it" && PlatformGlue.Save.Data.settings.lang == "it", "Retry completes");
            Assert.AreEqual("it", Loc.Language, "Retry must not reuse a cached failed table operation");
            Assert.IsTrue(Loc.Ready);
            Assert.AreEqual("it", PlatformGlue.Save.Data.settings.lang);
            Assert.AreNotEqual(title, Loc.Get("menu.title"));

            // Back-to-back choices may share a pending localization initialization. Only
            // the latest choice may replace strings or persist its setting.
            PlatformGlue.SetLanguageOverride("ru");
            PlatformGlue.SetLanguageOverride("en");
            float switchStarted = Time.realtimeSinceStartup;
            while ((Loc.Language != "en" || PlatformGlue.Save.Data.settings.lang != "en")
                && Time.realtimeSinceStartup - switchStarted < 10f) yield return null;
            Debug.Log($"[LocRecovery] rapid switch elapsed={Time.realtimeSinceStartup - switchStarted:F3}s selected={LocalizationSettings.SelectedLocale.Identifier.Code} language={Loc.Language}");
            Assert.AreEqual("en", Loc.Language);
            Assert.AreEqual("en", PlatformGlue.Save.Data.settings.lang);
            Assert.AreEqual(title, Loc.Get("menu.title"));
        }
    }
}
