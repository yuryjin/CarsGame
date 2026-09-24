using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class CarPaintTests
    {
        [Test]
        public void SetColorUpdatesOnlyPaintRenderers()
        {
            var root = new GameObject("car");
            var paintMaterial = MaterialNamed("CarPaint body");
            var otherMaterial = MaterialNamed("Glass");
            var paintRenderer = Renderer(root, "paint", paintMaterial);
            var otherRenderer = Renderer(root, "glass", otherMaterial);
            root.AddComponent<CarPaint>();
            var color = new Color(0.2f, 0.4f, 0.8f, 1f);
            try
            {
                root.GetComponent<CarPaint>().SetColor(color);
                AssertColor(color, ReadColor(paintRenderer));
                AssertColor(Color.clear, ReadColor(otherRenderer));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(paintMaterial); Object.DestroyImmediate(otherMaterial); }
        }

        [Test]
        public void RefreshFindsPaintRenderersAddedAfterAwake()
        {
            var root = new GameObject("car");
            var paintMaterial = MaterialNamed("CarPaint added later");
            root.AddComponent<CarPaint>();
            var renderer = Renderer(root, "late body", paintMaterial);
            var color = new Color(0.7f, 0.3f, 0.1f, 1f);
            try
            {
                root.GetComponent<CarPaint>().SetColor(color);
                AssertColor(color, ReadColor(renderer));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(paintMaterial); }
        }

        [Test]
        public void FlashAppliesHitColorAndCancelStopsIt()
        {
            var root = new GameObject("car");
            var material = MaterialNamed("CarPaint body");
            var renderer = Renderer(root, "body", material);
            var paint = root.AddComponent<CarPaint>();
            var hitColor = new Color(1f, 0.9f, 0.7f, 1f);
            try
            {
                paint.Flash(0.1f, hitColor);
                Assert.IsTrue(paint.IsFlashing);
                AssertColor(hitColor, ReadColor(renderer));
                paint.CancelFlash();
                Assert.IsFalse(paint.IsFlashing);
                Assert.IsFalse(paint.enabled);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(material); }
        }

        private static Material MaterialNamed(string name) => new Material(Shader.Find("Sprites/Default")) { name = name };

        private static MeshRenderer Renderer(GameObject parent, string name, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Color ReadColor(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-5f);
            Assert.AreEqual(expected.g, actual.g, 1e-5f);
            Assert.AreEqual(expected.b, actual.b, 1e-5f);
            Assert.AreEqual(expected.a, actual.a, 1e-5f);
        }
    }
}
