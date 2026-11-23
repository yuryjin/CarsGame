using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public class UiInteractionTests
    {
        [TestCase(1576f, 2)]
        [TestCase(1000f, 1)]
        public void ResultsKeepRewardAndStandingsReadable(float width, int scrollCount)
        {
            var owner = new GameObject("ResultsLayoutTest");
            owner.SetActive(false); // Build the view without starting a race or touching the save.
            var root = new GameObject("ResultsRoot", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, 976f);
            try
            {
                var menu = owner.AddComponent<MenuScreens>();
                var race = owner.GetComponent<RaceManager>();
                var results = new RaceResults();
                for (int i = 1; i <= 4; i++)
                    results.Entries.Add(new RaceResultEntry { Name = "Driver " + i, Position = i, IsPlayer = i == 3, Finished = true, TotalTime = 140 + i, BestLap = 45 });
                typeof(RaceManager).GetProperty("Results").SetValue(race, results);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var flow = owner.GetComponent<GameFlow>();
                typeof(GameFlow).GetField("race", flags).SetValue(flow, race);
                typeof(MenuScreens).GetField("flow", flags).SetValue(menu, flow);
                typeof(MenuScreens).GetField("race", flags).SetValue(menu, race);
                typeof(MenuScreens).GetMethod("BuildResults", flags).Invoke(menu, new object[] { (RectTransform)root.transform });
                Canvas.ForceUpdateCanvases();
                var scrolls = root.GetComponentsInChildren<ScrollRect>();
                Assert.AreEqual(scrollCount, scrolls.Length);
                foreach (var scroll in scrolls) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                var rows = root.GetComponentsInChildren<RectTransform>();
                var player = System.Array.Find(rows, r => r.name == "Result3");
                var reward = System.Array.Find(rows, r => r.name == "Reward");
                Assert.Greater(reward.rect.width, 300f, "The reward must have enough room beside the place badge.");
                var detail = (RectTransform)player.Find("Detail");
                var time = (RectTransform)player.Find("Time");
                var detailBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(player, detail);
                var timeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(player, time);
                Assert.LessOrEqual(detailBounds.max.x, timeBounds.min.x, "Race stats and finish time must not overlap.");
                Assert.Greater(detail.rect.width, 240f);
                Assert.AreEqual(4, System.Array.FindAll(rows, r => r.name.Length == 7 && r.name.StartsWith("Result") && char.IsDigit(r.name[6])).Length);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(owner); }
        }

        [Test]
        public void UnavailableActionsAreDisabledAndKeyboardFocusScrollsIntoView()
        {
            var root = new GameObject("UiTest", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)root.transform).sizeDelta = new Vector2(800f, 400f);
            try
            {
                var content = UiFactory.Scroll(root.transform, "List", Vector2.zero, Vector2.zero);
                var scroll = content.GetComponentInParent<ScrollRect>();
                var locked = UiFactory.Button(content, "Locked", "Locked", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                locked.gameObject.AddComponent<LayoutElement>().preferredHeight = 500f;
                Assert.IsFalse(locked.IsInteractable(), "A locked race or maxed upgrade must not accept input.");
                var action = UiFactory.Button(content, "Last", "Go", () => { }, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                action.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                scroll.Rebuild(CanvasUpdate.PostLayout);
                scroll.verticalNormalizedPosition = 1f;
                Assert.IsTrue(action.IsInteractable());
                Assert.Greater(content.rect.height, scroll.viewport.rect.height);
                action.GetComponent<UiButtonFx>().OnSelect(null);
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)action.transform);
                Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - 1f, "Keyboard selection must reveal the last action.");
                Assert.LessOrEqual(bounds.max.y, scroll.viewport.rect.yMax + 1f);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
