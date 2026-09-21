using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public class UiInteractionTests
    {
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
