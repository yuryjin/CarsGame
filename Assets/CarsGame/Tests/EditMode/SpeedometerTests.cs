using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public class SpeedometerTests
    {
        [Test]
        public void TextCanBeBuiltInsideAnInactiveHud()
        {
            var root = new GameObject("HiddenHud", typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 80f);
            root.SetActive(false);
            try
            {
                var label = UiFactory.Text(root.transform, "Label", "HP 100", 26f, TMPro.TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Assert.IsNotNull(label.font);
                root.SetActive(true);
                label.ForceMeshUpdate();
                Assert.Greater(label.textInfo.characterCount, 0);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0f, 0f)]
        [TestCase(-120f, 120f)]
        [TestCase(500f, 240f)]
        public void DialStaysInsideItsPanelAndClampsSpeed(float speed, float expectedSpeed)
        {
            var root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("Dial", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var dial = go.AddComponent<SpeedometerGraphic>();
            dial.rectTransform.sizeDelta = new Vector2(244f, 224f);
            try
            {
                dial.SetSpeed(speed, false);
                dial.Rebuild(CanvasUpdate.PreRender);
                var mesh = dial.canvasRenderer.GetMesh();
                Vector3[] actual = mesh.vertices;
                Assert.Greater(actual.Length, 0, "The speedometer must produce visible geometry.");
                Rect bounds = dial.rectTransform.rect;
                foreach (Vector3 point in actual)
                {
                    Assert.That(point.x, Is.InRange(bounds.xMin, bounds.xMax));
                    Assert.That(point.y, Is.InRange(bounds.yMin, bounds.yMax));
                }

                dial.SetSpeed(expectedSpeed, false);
                dial.Rebuild(CanvasUpdate.PreRender);
                mesh = dial.canvasRenderer.GetMesh();
                CollectionAssert.AreEqual(actual, mesh.vertices, "Reverse and over-range readings must use the same dial positions.");

                dial.SetSpeed(expectedSpeed, true);
                dial.Rebuild(CanvasUpdate.PreRender);
                mesh = dial.canvasRenderer.GetMesh();
                CollectionAssert.AreEqual(actual, mesh.vertices, "Nitro changes the accent, not the speed scale.");
                CollectionAssert.Contains(mesh.colors32, (Color32)SpeedometerGraphic.BoostColor);
                CollectionAssert.DoesNotContain(mesh.colors32, (Color32)SpeedometerGraphic.DraftColor);
                foreach (float charge in new[] { .25f, .75f, .5f, 0f })
                {
                    dial.SetSpeed(expectedSpeed, true, charge);
                    dial.Rebuild(CanvasUpdate.PreRender);
                    int green = System.Array.FindAll(dial.canvasRenderer.GetMesh().colors32,
                        c => c.Equals((Color32)SpeedometerGraphic.DraftColor)).Length;
                    Assert.AreEqual(4 * Mathf.CeilToInt(80 * charge), green);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
