using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Daily;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class DailyRolloverPlayTests
    {
        private Func<long> timeSource;
        private long now;
        private float capture;
        private string saved;
        private GameFlow flow;
        private MenuScreens menu;

        [SetUp] public void Setup()
        {
            timeSource = DailyRace.UnixMsSource;
            now = DailyChallenge.DayStartUnixMs(1001) - 90000;
            DailyRace.UnixMsSource = () => now;
            capture = Time.captureDeltaTime;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            PauseController.ResetAll();
        }

        [TearDown] public void Cleanup()
        {
            DailyRace.UnixMsSource = timeSource;
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (flow != null) flow.EnterMenu();
            Time.captureDeltaTime = capture;
            PauseController.ResetAll();
        }

        private IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            menu = Object.FindAnyObjectByType<MenuScreens>();
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            PlatformGlue.Save.Data.Local(PlatformGlue.Save.Data.deviceId).races = 1;
            menu.Show("main");
            yield return null; yield return null;
        }

        private static TMP_Text DailyLabel(string name) => Array.Find(Object.FindObjectsByType<TMP_Text>(),
            t => t.name == name && t.transform.parent.name == "DailyCard");
        private static Button PlayDaily() => Array.Find(Object.FindObjectsByType<Button>(), b => b.name == "PlayDaily");
        private static IEnumerator LetMenuRefresh() { yield return new WaitForSecondsRealtime(1.25f); }

        [UnityTest, Timeout(180000)]
        public IEnumerator VisibleDailyCountdownUpdatesWithoutRebuildingTheMenu()
        {
            yield return Boot();
            var foot = DailyLabel("Foot");
            Assert.NotNull(foot);
            StringAssert.Contains(Loc.Format("daily.new_in", "h", 0, "m", 1), foot.text);
            now += 60000;
            yield return LetMenuRefresh();
            Assert.AreSame(foot, DailyLabel("Foot"), "A minute tick should update only the countdown label.");
            StringAssert.Contains(Loc.Format("daily.new_in", "h", 0, "m", 0), foot.text);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MidnightRefreshesTheDailyCardAndPreservesMenuFocus()
        {
            yield return Boot();
            var oldCard = PlayDaily().transform.parent;
            EventSystem.current.SetSelectedGameObject(PlayDaily().gameObject);
            string progress = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
            now = DailyChallenge.DayStartUnixMs(1001) + 1000;
            yield return LetMenuRefresh();
            Assert.AreNotSame(oldCard, PlayDaily().transform.parent, "An open daily card must refresh its rules at UTC midnight.");
            Assert.AreEqual("PlayDaily", EventSystem.current.currentSelectedGameObject?.name);
            StringAssert.Contains(Loc.Format("daily.new_in", "h", 23, "m", 59), DailyLabel("Foot").text);
            Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress), "Refreshing a date cannot grant progression.");
            PlayDaily().onClick.Invoke();
            Assert.AreEqual(1001, DailyRace.DayOf(RaceSetup.Event));
            var started = RaceSetup.Event;
            now += 86400000;
            yield return LetMenuRefresh();
            Assert.AreSame(started, RaceSetup.Event, "A running daily race retains the day it started on.");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MidnightPreservesScrollWhenFocusRemainsOnAnEarlierControl()
        {
            yield return Boot();
            var scroll = Array.Find(Object.FindObjectsByType<ScrollRect>(), s => s.name == "MainScroll");
            Assert.NotNull(scroll);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0.2f;
            yield return null; yield return null;
            float position = scroll.verticalNormalizedPosition;
            Debug.Log($"[DailyScroll] before value={position} content={scroll.content.rect} viewport={scroll.viewport.rect} anchored={scroll.content.anchoredPosition}");
            now = DailyChallenge.DayStartUnixMs(1001) + 1000;
            yield return LetMenuRefresh();
            var rebuilt = Array.Find(Object.FindObjectsByType<ScrollRect>(), s => s.name == "MainScroll");
            Assert.AreNotSame(scroll, rebuilt);
            Debug.Log($"[DailyScroll] after value={rebuilt.verticalNormalizedPosition} content={rebuilt.content.rect} viewport={rebuilt.viewport.rect} anchored={rebuilt.content.anchoredPosition}");
            Assert.AreEqual(position, rebuilt.verticalNormalizedPosition, 0.001f, "Refreshing the daily rules must preserve a manually scrolled menu.");
        }
    }
}
