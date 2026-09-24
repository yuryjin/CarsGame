using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class PlayerCosmeticsTests
    {
        [Test]
        public void MissingOrUnownedSelectionsFallBackToDefaults()
        {
            Assert.AreEqual(Cosmetics.DefaultPaint, PlayerCosmetics.SelectedPaint(null));
            Assert.AreEqual(Cosmetics.DefaultLivery, PlayerCosmetics.SelectedLivery(null));

            var progress = new ProgressData { paint = "ocean", livery = "stripe" };
            Assert.AreEqual(Cosmetics.DefaultPaint, PlayerCosmetics.SelectedPaint(progress));
            Assert.AreEqual(Cosmetics.DefaultLivery, PlayerCosmetics.SelectedLivery(progress));
        }

        [Test]
        public void OwnedSelectionsAndStripePaintAreUsed()
        {
            var progress = new ProgressData
            {
                paint = "ocean", livery = "stripe", stripe = "sun",
                cosmetics = new System.Collections.Generic.List<string> { "paint:ocean", "livery:stripe", "paint:sun" }
            };

            Assert.AreEqual("ocean", PlayerCosmetics.SelectedPaint(progress));
            Assert.AreEqual("stripe", PlayerCosmetics.SelectedLivery(progress));
            AssertColor(PlayerCosmetics.PaintColor("sun"), PlayerCosmetics.StripeColor(progress));
            Cosmetics.ContrastStripe(0.92f, 0.06f, 0.10f, out float r, out float g, out float b);
            AssertColor(new Color(r, g, b), PlayerCosmetics.StripeColor(null));
        }

        [Test]
        public void ApplyRepaintsExistingCarPaintAndAddsRequestedLivery()
        {
            var car = new GameObject("test car");
            var body = new GameObject("body");
            body.transform.SetParent(car.transform);
            var renderer = body.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Sprites/Default")) { name = "TestCarPaint" };
            renderer.sharedMaterial = material;
            var paint = car.AddComponent<CarPaint>();
            var progress = new ProgressData
            {
                paint = "ocean", livery = "stripe",
                cosmetics = new System.Collections.Generic.List<string> { "paint:ocean", "livery:stripe" }
            };

            try
            {
                PlayerCosmetics.Apply(car, progress);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                AssertColor(PlayerCosmetics.PaintColor("ocean"), block.GetColor("_BaseColor"));
                Assert.AreEqual(LiveryPattern.CenterStripe, car.GetComponent<CarLivery>().Pattern);
                Assert.DoesNotThrow(() => PlayerCosmetics.Apply(null, progress));
            }
            finally
            {
                Object.DestroyImmediate(car);
                Object.DestroyImmediate(material);
            }
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
