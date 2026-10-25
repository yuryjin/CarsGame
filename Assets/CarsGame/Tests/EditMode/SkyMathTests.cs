using NUnit.Framework;
using UnityEngine;
using RacingGame.World;

namespace RacingGame.Tests
{
    public class SkyMathTests
    {
        [Test]
        public void GradientSelectsZenithHorizonAndGround()
        {
            var p = NeutralSky();
            AssertColor(p.Zenith, SkyMath.Evaluate(Vector3.up, p));
            AssertColor(p.Horizon, SkyMath.Evaluate(Vector3.forward, p));
            AssertColor(p.Ground, SkyMath.Evaluate(Vector3.down, p));
        }

        [Test]
        public void SunAddsLightOnlyWhenAboveHorizonAndMoonAddsItsOwnLight()
        {
            var p = NeutralSky();
            p.SunDir = Vector3.up; p.SunSize1mCos = 0.01f; p.SunColor = Color.red;
            Color lit = SkyMath.Evaluate(Vector3.up, p);
            Assert.Greater(lit.r, p.Zenith.r);
            AssertColor(p.Ground, SkyMath.Evaluate(Vector3.down, p));

            p.SunColor = Color.clear; p.MoonDir = Vector3.up; p.MoonSize1mCos = 0.01f; p.MoonColor = Color.blue;
            Assert.Greater(SkyMath.Evaluate(Vector3.up, p).b, p.Zenith.b);
        }

        [Test]
        public void CubeFacesPointAlongTheirExpectedAxes()
        {
            AssertVector(Vector3.right, SkyMath.CubeDirection(CubemapFace.PositiveX, 0.5f, 0.5f));
            AssertVector(Vector3.left, SkyMath.CubeDirection(CubemapFace.NegativeX, 0.5f, 0.5f));
            AssertVector(Vector3.up, SkyMath.CubeDirection(CubemapFace.PositiveY, 0.5f, 0.5f));
            AssertVector(Vector3.down, SkyMath.CubeDirection(CubemapFace.NegativeY, 0.5f, 0.5f));
            AssertVector(Vector3.forward, SkyMath.CubeDirection(CubemapFace.PositiveZ, 0.5f, 0.5f));
            AssertVector(Vector3.back, SkyMath.CubeDirection(CubemapFace.NegativeZ, 0.5f, 0.5f));
        }

        [Test]
        public void DiscSizeMatchesAngularRadius()
        {
            Assert.AreEqual(0f, SkyMath.DiscSize(0f), 1e-6f);
            Assert.AreEqual(1f, SkyMath.DiscSize(90f), 1e-6f);
        }

        [Test]
        public void PresetConvertsAuthoredColorsAndDiscRadiiToShaderParameters()
        {
            var preset = ScriptableObject.CreateInstance<SkyPreset>();
            try
            {
                var p = preset.ToParams();
                AssertColor(preset.zenith.linear, p.Zenith);
                AssertColor(preset.sunDiscColor.linear, p.SunColor);
                Assert.AreEqual(SkyMath.DiscSize(preset.sunAngularRadius), p.SunSize1mCos, 1e-6f);
                Assert.Less(Vector3.Distance(preset.SunDirection.normalized, p.SunDir.normalized), 1e-5f);
                Assert.Less(Vector3.Distance(preset.MoonDirection.normalized, p.MoonDir.normalized), 1e-5f);
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void DefaultReflectionMatchesTheVisibleSky()
        {
            var preset = ScriptableObject.CreateInstance<SkyPreset>();
            try
            {
                foreach (var direction in new[] { Vector3.up, Vector3.forward, Vector3.down, new Vector3(1f, .4f, -1f) })
                    AssertColor(SkyMath.Evaluate(direction, preset.ToParams()), SkyMath.Evaluate(direction, preset.ToReflectionParams()));
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void ReflectionFillRevealsMetalWithoutBrighteningTheVisibleSky()
        {
            var preset = ScriptableObject.CreateInstance<SkyPreset>();
            try
            {
                preset.sunDiscColor = preset.moonColor = Color.black;
                preset.zenith = preset.horizon = preset.ground = Color.black;
                preset.reflectionFill = new Color(.55f, .6f, .7f);
                var reflection = preset.ToReflectionParams();
                Color top = SkyMath.Evaluate(Vector3.up, reflection);
                Color middle = SkyMath.Evaluate(Vector3.forward, reflection);
                Color bottom = SkyMath.Evaluate(Vector3.down, reflection);
                Assert.Greater(top.grayscale, middle.grayscale);
                Assert.Greater(middle.grayscale, bottom.grayscale);
                Assert.Greater(bottom.grayscale, 0f);
                AssertColor(Color.black, SkyMath.Evaluate(Vector3.up, preset.ToParams()));
                AssertColor(Color.black, SkyMath.Evaluate(Vector3.forward, preset.ToParams()));
                AssertColor(Color.black, SkyMath.Evaluate(Vector3.down, preset.ToParams()));
                preset.reflectionFill = Color.black;
                AssertColor(Color.black, SkyMath.Evaluate(Vector3.up, preset.ToReflectionParams()));
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void FillCubemapWritesEveryFaceWithOpaqueClampedColors()
        {
            var cube = new Cubemap(4, TextureFormat.RGBA32, false);
            var p = NeutralSky();
            p.Zenith = new Color(2f, 0.2f, 0.1f, 0.2f);
            p.Horizon = p.Ground = p.Zenith;
            try
            {
                SkyMath.FillCubemap(cube, p, new Color[1]); // wrong-sized scratch must be replaced
                foreach (CubemapFace face in System.Enum.GetValues(typeof(CubemapFace)))
                {
                    if ((int)face < 0 || (int)face >= 6) continue;
                    foreach (var color in cube.GetPixels(face))
                    {
                        Assert.AreEqual(1f, color.r, 1f / 255f);
                        Assert.AreEqual(1f, color.a, 1f / 255f);
                    }
                }
            }
            finally { Object.DestroyImmediate(cube); }
        }

        private static SkyParams NeutralSky() => new SkyParams
        {
            Zenith = new Color(0.1f, 0.2f, 0.3f, 1f),
            Horizon = new Color(0.4f, 0.5f, 0.6f, 1f),
            Ground = new Color(0.7f, 0.8f, 0.9f, 1f),
            HorizonExp = 1f,
            SunDir = Vector3.down,
            SunSize1mCos = 0.01f,
            SunGlowPower = 1f,
            MoonDir = Vector3.down,
            MoonSize1mCos = 0.01f
        };

        private static void AssertColor(Color e, Color a)
        {
            Assert.AreEqual(e.r, a.r, 1e-5f); Assert.AreEqual(e.g, a.g, 1e-5f);
            Assert.AreEqual(e.b, a.b, 1e-5f); Assert.AreEqual(1f, a.a, 1e-5f);
        }

        private static void AssertVector(Vector3 e, Vector3 a) => Assert.Less(Vector3.Distance(e, a), 1e-5f);
    }
}
