using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class CupProgressionPlayTests
    {
        private string before;
        private GameFlow flow;
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (before != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(before, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll();
        }

        private static Button Button(string name) => System.Array.Find(Object.FindObjectsByType<Button>(), b => b.name == name);

        private static void SeedFailedPodium()
        {
            var p = PlatformGlue.Save.Data.progress;
            p.bests.RemoveAll(b => b.eventId == "e1" || b.eventId == "e2" || b.eventId == "e3");
            p.bests.Add(new EventBest { eventId = "e1", bestTimeMs = 161068, stars = 1 });
            p.bests.Add(new EventBest { eventId = "e2", bestTimeMs = 187895, stars = 4 });
            if (!p.unlockedEvents.Contains("e2")) p.unlockedEvents.Add("e2");
            p.unlockedEvents.Remove("e3");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CupSuggestsUnlockingTheNextRaceBeforeFarmingOldStars()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var menus = Object.FindAnyObjectByType<MenuScreens>();
            SeedFailedPodium();
            menus.Show("events"); yield return null;
            Button("CupStart_rookie").onClick.Invoke();
            Assert.AreEqual("e2", flow.CurrentEvent.eventId, "A fifth-place e2 finish needs a podium retry to open e3");
            while (race.State == RaceState.Countdown) yield return null;
            race.QaFinishNow(); yield return null;
            // Recreate the browser-observed fifth-place result for recommendation rendering.
            SeedFailedPodium();
            race.Results.Player.Position = 5;
            menus.Show("results"); yield return null;
            var goal = System.Array.Find(Object.FindObjectsByType<TMPro.TMP_Text>(), t => t.name == "Goal");
            Assert.NotNull(goal);
            StringAssert.Contains(Loc.Get(flow.CurrentEvent.nameKey), goal.text);
            StringAssert.Contains(Loc.Format("goals.place", "n", 3), goal.text);
            StringAssert.Contains(Loc.Get("results.retry"), Button("ResultAction0").GetComponentInChildren<TMPro.TMP_Text>().text);
            // Initial keyboard focus used to scroll the reward above the viewport.
            var reward = System.Array.Find(Object.FindObjectsByType<TMPro.TMP_Text>(), t => t.name == "Reward");
            var scroll = reward.GetComponentInParent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, reward.rectTransform);
            Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - 1f);
            Assert.LessOrEqual(bounds.max.y, scroll.viewport.rect.yMax + 1f, "Reward must be visible on arrival");
            var action = (RectTransform)Button("ResultAction0").transform;
            var actionPosition = action.position;
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Assert.Less(Vector3.Distance(actionPosition, action.position), 1f, "Primary actions must remain available while reading results");
            Button("ResultAction0").onClick.Invoke();
            Assert.AreEqual("e2", flow.CurrentEvent.eventId);

            flow.EnterMenu();
            PlatformGlue.Save.Data.progress.unlockedEvents.Add("e3");
            menus.Show("events"); yield return null;
            Button("CupStart_rookie").onClick.Invoke();
            Assert.AreEqual("e3", flow.CurrentEvent.eventId, "An open, unplayed race takes priority");

            flow.EnterMenu();
            PlatformGlue.Save.Data.progress.bests.Add(new EventBest { eventId = "e3", bestTimeMs = 180000, stars = 4 });
            menus.Show("events"); yield return null;
            Button("CupStart_rookie").onClick.Invoke();
            Assert.AreEqual("e1", flow.CurrentEvent.eventId, "Once every race is open and finished, missing stars become the goal");
        }
    }
}
