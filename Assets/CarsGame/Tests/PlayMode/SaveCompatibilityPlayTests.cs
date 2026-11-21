using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class SaveCompatibilityPlayTests
    {
        private SaveService original;
        private sealed class Store : ILocalSaveStore
        {
            public string Json = "{\"version\":999,\"futureProgress\":\"preserve\"}";
            public int Writes;
            public string Read(LocalSaveSlot slot = LocalSaveSlot.Profile) => slot == LocalSaveSlot.Profile ? Json : null;
            public void Write(string json, LocalSaveSlot slot = LocalSaveSlot.Profile) { Json = json; Writes++; }
        }
        private static void SetSave(SaveService save) => typeof(PlatformGlue).GetProperty("Save").SetValue(null, save);

        [TearDown] public void Cleanup()
        {
            if (original != null) SetSave(original);
            var flow = Object.FindAnyObjectByType<GameFlow>();
            if (flow != null) flow.EnterMenu();
            PauseController.ResetAll();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NewerLocalSaveExplainsTheRequiredUpdateAndCannotStartUnsavablePlay()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            original = PlatformGlue.Save;
            var store = new Store();
            var service = new SaveService(store, () => null, () => Time.realtimeSinceStartup, "old-client-ui-test");
            var loading = service.LoadAsync();
            while (!loading.IsCompleted) yield return null;
            Assert.IsFalse(loading.IsFaulted);
            SetSave(service);
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            var flow = Object.FindAnyObjectByType<GameFlow>();
            foreach (var screen in new[] { "main", "settings", "garage", "practice", "events" })
            {
                menu.Show(screen); yield return null;
                var message = GameObject.Find("SaveVersionMessage").GetComponent<TMPro.TMP_Text>();
                Assert.AreEqual(Loc.Get("save.newer.message"), message.text);
                Assert.IsFalse(message.isTextTruncated);
                Assert.IsNull(GameObject.Find("Coins"), "An unreadable profile must not be presented as having zero coins");
                Assert.AreEqual(0, menu.GetComponentsInChildren<Button>().Length, "The compatibility screen does not offer purchases or play");
            }
            flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), null);
            Assert.AreEqual(FlowState.Menu, flow.State);
            Assert.IsFalse(flow.StartPractice(PracticeLesson.Drift));
            Assert.IsFalse(flow.StartTestDrive("classic"));
            Assert.IsFalse(flow.IsTestDrive, "A rejected request cannot leave a half-started test drive");
            service.MarkDirty(); service.Tick();
            Assert.AreEqual(0, store.Writes);
            StringAssert.Contains("preserve", store.Json);
        }
    }
}
