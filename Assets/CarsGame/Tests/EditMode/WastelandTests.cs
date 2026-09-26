using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public sealed class WastelandTests
    {
        [Test]
        public void WastelandIsASeparateDrivableCourse()
        {
            var layout = new WastelandLayout();
            var space = new StarRingLayout();
            Assert.Greater(layout.Length, 1000f);
            Assert.Greater(Vector3.Distance(layout.Points[100], space.Points[100]), 10f, "separate route from the space map");
            Assert.Less(Vector3.Distance(layout.Points[layout.Points.Length - 1], layout.Points[0]), WastelandLayout.Step * 1.5f);

            var root = new GameObject("WastelandTestPath");
            WastelandTrack map = null;
            try
            {
                for (int i = 0; i < 64; i++)
                {
                    var point = new GameObject("Point").transform;
                    point.SetParent(root.transform);
                    float a = i * Mathf.PI * 2f / 64f;
                    point.position = new Vector3(Mathf.Sin(a) * 80f, 0f, Mathf.Cos(a) * 80f);
                }
                var path = root.AddComponent<TrackPath>();
                float originalWidth = path.halfWidth;
                map = WastelandTrack.Create(path);
                Physics.SyncTransforms();
                Assert.AreEqual(WastelandLayout.HalfWidth, path.halfWidth);
                int mask = 1 << LayerMask.NameToLayer("Track");
                bool Deck(float d, float lane) => Physics.Raycast(path.Position(d) + path.Right(d) * lane + Vector3.up * 4f,
                    Vector3.down, 8f, mask);
                foreach (float lane in new[] { -8f, 0f, 8f })
                {
                    Assert.IsTrue(Deck(layout.GapStart - 3f, lane));
                    Assert.IsFalse(Deck((layout.GapStart + layout.GapEnd) * 0.5f, lane), "full-width gap");
                    Assert.IsTrue(Deck(layout.GapEnd + 3f, lane));
                }
                float tunnel = (layout.TunnelStart + layout.TunnelEnd) * 0.5f;
                Assert.IsTrue(Physics.Raycast(path.Position(tunnel) + Vector3.up * 5f, Vector3.up,
                    out var ceiling, 10f, mask));
                Assert.IsNotNull(ceiling.collider.GetComponent<GravitySurface>());
                Assert.AreEqual(2, map.Pads.Count);
                Assert.IsNotNull(path.RespawnGuard);
                Assert.LessOrEqual(layout.SafeRespawn(layout.GapStart + 2f), layout.GapStart - WastelandLayout.JumpRunUp);
                map.Restore();
                Assert.IsFalse(path.HasLayout);
                Assert.AreEqual(originalWidth, path.halfWidth);
            }
            finally
            {
                if (map != null) Object.DestroyImmediate(map.gameObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
