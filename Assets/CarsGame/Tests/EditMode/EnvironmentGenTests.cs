using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Track;
using RacingGame.World;
using RacingGame.World.Gen;

namespace RacingGame.Tests
{
    public class EnvironmentGenTests
    {
        private static TrackSpline Circle(float radius, int n)
        {
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; pts[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius); }
            return new TrackSpline(pts);
        }

        [Test]
        public void TerrainIsFlatInsideTheCorridor()
        {
            var s = new TerrainSettings();
            var noise = new PropNoise(s.seed);
            for (float d = 0f; d < s.flatRadius; d += 5f)
                for (int k = 0; k < 8; k++)
                {
                    float h = TerrainMeshGen.Height(k * 37.3f, d * 1.7f, d, false, s, noise, out _);
                    Assert.AreEqual(s.baseY, h, 1e-4f, $"height must stay at baseY at {d} m from the track");
                }
            float far = TerrainMeshGen.Height(200f, 200f, 300f, false, s, noise, out float hill);
            Assert.GreaterOrEqual(far, s.baseY);
            Assert.That(hill, Is.InRange(0f, 1f));
        }

        [Test]
        public void NoiseIsDeterministicAndBounded()
        {
            var a = new PropNoise(7); var b = new PropNoise(7); var c = new PropNoise(8);
            float sa = 0f, sb = 0f, sc = 0f;
            for (int i = 0; i < 200; i++)
            {
                float x = i * 0.37f, y = i * 0.11f;
                float va = a.Fbm(x, y, 0.1f, 4), vb = b.Fbm(x, y, 0.1f, 4), vc = c.Fbm(x, y, 0.1f, 4);
                Assert.AreEqual(va, vb, 1e-6f);
                Assert.That(va, Is.InRange(-1f, 1f));
                Assert.That(a.Value(x, y), Is.InRange(0f, 1f));
                sa += va; sb += vb; sc += vc;
            }
            Assert.AreNotEqual(sa, sc, "different seeds must produce different fields");
        }

        [Test]
        public void InsideLoopDetectsInfield()
        {
            var spline = Circle(100f, 40);
            Assert.IsTrue(TerrainMeshGen.InsideLoop(spline, 0f, 0f));
            Assert.IsTrue(TerrainMeshGen.InsideLoop(spline, 60f, -30f));
            Assert.IsFalse(TerrainMeshGen.InsideLoop(spline, 150f, 0f));
            Assert.AreEqual(50f, TerrainMeshGen.DistanceToTrack(spline, new Vector3(50f, 0f, 0f)), 1.5f);
        }

        [Test]
        public void ChunkWriterSplitsBelowUInt16Limit()
        {
            var w = new MeshChunkWriter("t");
            for (int i = 0; i < 30000; i++) w.AddTriangle(new Vector3(i, 0, 0), new Vector3(i, 1, 0), new Vector3(i, 0, 1), new Color32(10, 20, 30, 0));
            var meshes = w.Finish();
            Assert.GreaterOrEqual(meshes.Count, 2);
            int total = 0;
            foreach (var m in meshes) { Assert.LessOrEqual(m.vertexCount, 65000); Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, m.indexFormat); total += m.vertexCount; }
            Assert.AreEqual(90000, total);
            foreach (var m in meshes) Object.DestroyImmediate(m);
        }

        [Test]
        public void SkyMathMatchesGradientAtHorizonAndZenith()
        {
            var p = new SkyParams { Zenith = Color.blue, Horizon = Color.white, Ground = Color.black, HorizonExp = 0.5f, SunDir = new Vector3(0f, -1f, 0f), SunColor = Color.black, SunSize1mCos = 0.001f, SunGlowPower = 24f };
            var h = SkyMath.Evaluate(new Vector3(1f, 0f, 0f), p);
            Assert.AreEqual(1f, h.r, 1e-4f); Assert.AreEqual(1f, h.b, 1e-4f);
            var z = SkyMath.Evaluate(Vector3.up, p);
            Assert.AreEqual(0f, z.r, 1e-4f); Assert.AreEqual(1f, z.b, 1e-4f);
            Assert.AreEqual(0f, SkyMath.DiscSize(0f), 1e-6f);
            Assert.Greater(SkyMath.DiscSize(3f), SkyMath.DiscSize(1f));
            foreach (CubemapFace f in System.Enum.GetValues(typeof(CubemapFace)))
            {
                if (f == CubemapFace.Unknown) continue;
                var dir = SkyMath.CubeDirection(f, 0.5f, 0.5f).normalized;
                Assert.AreEqual(1f, Mathf.Abs(dir.x) + Mathf.Abs(dir.y) + Mathf.Abs(dir.z), 1e-4f, "face centre must be an axis direction");
            }
        }

        [Test]
        public void PropLayoutKeepsTheCorridorClear()
        {
            var spline = Circle(120f, 60);
            var authored = new[] { new Vector3(125f, 0f, 0f), new Vector3(150f, 0f, 0f) };   // first one is inside the corridor
            var layout = PropLayout.Build(spline, authored, 40f, 1, groveRingMin: 40f, groveRingMax: 120f, newTreeCap: 30);
            foreach (var t in layout.Trees) Assert.GreaterOrEqual(t.DistToTrack, PropLayout.ClearCorridor);
            Assert.AreEqual(6, PropLayout.SharpestCorners(spline, 6, 60f).Count);
            Assert.GreaterOrEqual(layout.Lamps.Count, 8);
            foreach (var l in layout.Lamps) Assert.AreEqual(120f + 8.6f, new Vector2(l.Position.x, l.Position.z).magnitude, 1.5f);
        }
    }
}
