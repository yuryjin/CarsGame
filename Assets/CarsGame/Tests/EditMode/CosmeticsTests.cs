using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame.Tests
{
    public class CosmeticsTests
    {
        [Test]
        public void IdsAreUniqueAndDefaultsFree()
        {
            var ids = new HashSet<string>();
            foreach (var p in Cosmetics.Paints) Assert.IsTrue(ids.Add("p" + p.Id), p.Id);
            foreach (var l in Cosmetics.Liveries) Assert.IsTrue(ids.Add("l" + l.Id), l.Id);
            Assert.IsTrue(Cosmetics.OwnsPaint(new List<string>(), Cosmetics.DefaultPaint));
            Assert.IsTrue(Cosmetics.OwnsLivery(null, Cosmetics.DefaultLivery));
        }

        [Test]
        public void BuyingNeedsCoinsAndHappensOnce()
        {
            var owned = new List<string>();
            Assert.IsFalse(Cosmetics.TryBuy(owned, false, "snow", 100, out int cost)); Assert.AreEqual(120, cost);
            Assert.IsTrue(Cosmetics.TryBuy(owned, false, "snow", 500, out cost)); Assert.AreEqual(120, cost);
            Assert.IsTrue(Cosmetics.OwnsPaint(owned, "snow"));
            Assert.IsFalse(Cosmetics.TryBuy(owned, false, "snow", 500, out _), "already owned");
            Assert.IsFalse(Cosmetics.TryBuy(owned, false, "teal", 5000, out _), "rewarded paints are not for sale");
            Assert.IsFalse(Cosmetics.TryBuy(owned, true, "nope", 5000, out _));
        }

        [Test]
        public void RewardedItemsAreGrantedOnlyOnce()
        {
            var owned = new List<string>();
            Assert.IsTrue(Cosmetics.GrantRewarded(owned, true, "hood"));
            Assert.IsFalse(Cosmetics.GrantRewarded(owned, true, "hood"));
            Assert.IsFalse(Cosmetics.GrantRewarded(owned, false, "snow"), "coin paints are not granted by ads");
            Assert.IsTrue(Cosmetics.OwnsLivery(owned, "hood"));
        }

        [Test]
        public void ContrastStripeFlipsOnLightPaint()
        {
            Cosmetics.ContrastStripe(0.95f, 0.95f, 0.95f, out float r, out _, out _); Assert.Less(r, 0.2f);
            Cosmetics.ContrastStripe(0.1f, 0.1f, 0.1f, out r, out _, out _); Assert.Greater(r, 0.8f);
        }

        // A 2×1×4 box body (x right, y up, z forward), 12 triangles, outward winding.
        private static void BoxMesh(out List<Vector3> v, out List<int> t)
        {
            var vs = new List<Vector3>(); var ts = new List<int>();
            v = vs; t = ts;
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int s = vs.Count; vs.Add(a); vs.Add(b); vs.Add(c); vs.Add(d); ts.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
            float x = 1f, y0 = 0f, y1 = 1f, z = 2f;
            Quad(new Vector3(-x, y1, -z), new Vector3(-x, y1, z), new Vector3(x, y1, z), new Vector3(x, y1, -z));   // top
            Quad(new Vector3(-x, y0, -z), new Vector3(x, y0, -z), new Vector3(x, y0, z), new Vector3(-x, y0, z));   // bottom
            Quad(new Vector3(x, y0, -z), new Vector3(x, y1, -z), new Vector3(x, y1, z), new Vector3(x, y0, z));     // right
            Quad(new Vector3(-x, y0, -z), new Vector3(-x, y0, z), new Vector3(-x, y1, z), new Vector3(-x, y1, -z)); // left
            Quad(new Vector3(-x, y0, z), new Vector3(x, y0, z), new Vector3(x, y1, z), new Vector3(-x, y1, z));     // front
            Quad(new Vector3(-x, y0, -z), new Vector3(-x, y1, -z), new Vector3(x, y1, -z), new Vector3(x, y0, -z)); // back
        }

        private static float Area(List<Vector3> v, List<int> t, System.Func<Vector3, bool> faceFilter)
        {
            float a = 0f;
            for (int i = 0; i < t.Count; i += 3)
            {
                var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (faceFilter(n.normalized)) a += n.magnitude * 0.5f;
            }
            return a;
        }

        [Test]
        public void CenterStripeCoversTheTopNoseAndTailButNotTheSides()
        {
            BoxMesh(out var v, out var t);
            var bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 4f));
            var ov = new List<Vector3>(); var on = new List<Vector3>(); var ot = new List<int>();
            int tris = LiveryClipper.Build(v, t, LiveryClipper.RegionsFor(LiveryPattern.CenterStripe, bounds), 0.01f, ov, on, ot);
            Assert.Greater(tris, 0);
            float top = Area(ov, ot, n => n.y > 0.9f), sides = Area(ov, ot, n => Mathf.Abs(n.x) > 0.9f), bottom = Area(ov, ot, n => n.y < -0.9f);
            Assert.AreEqual(0.26f * 4f, top, 1e-3f, "stripe 26 % of the width, full length");
            Assert.AreEqual(0f, sides, 1e-6f); Assert.AreEqual(0f, bottom, 1e-6f);
            Assert.Greater(Area(ov, ot, n => n.z > 0.9f), 0f, "runs down the nose");
            for (int i = 0; i < ov.Count; i++) if (on[i].y > 0.9f) Assert.AreEqual(1.01f, ov[i].y, 1e-4f, "pushed out along the normal");
            Assert.AreEqual(ov.Count, on.Count);
        }

        [Test]
        public void SideBandOnlyOnFlanksAndTwinStripesAreTwo()
        {
            BoxMesh(out var v, out var t);
            var bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 4f));
            var ov = new List<Vector3>(); var on = new List<Vector3>(); var ot = new List<int>();
            LiveryClipper.Build(v, t, LiveryClipper.RegionsFor(LiveryPattern.SideBand, bounds), 0.01f, ov, on, ot);
            Assert.AreEqual(0f, Area(ov, ot, n => n.y > 0.9f), 1e-6f);
            Assert.AreEqual(2f * 0.14f * 4f * 0.84f, Area(ov, ot, n => Mathf.Abs(n.x) > 0.9f), 1e-3f);

            ov.Clear(); on.Clear(); ot.Clear();
            LiveryClipper.Build(v, t, LiveryClipper.RegionsFor(LiveryPattern.TwinStripes, bounds), 0.01f, ov, on, ot);
            Assert.AreEqual(2f * 0.13f * 4f, Area(ov, ot, n => n.y > 0.9f), 1e-3f);
            foreach (var p in ov) Assert.Greater(Mathf.Abs(p.x), 0.16f, "gap between the stripes");
        }
    }
}
