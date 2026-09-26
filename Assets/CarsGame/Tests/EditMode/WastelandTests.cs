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
            Assert.AreEqual(3, layout.Tunnels.Length);
            for (int i = 0; i < layout.Tunnels.Length; i++)
            {
                var t = layout.Tunnels[i];
                Assert.Greater(t.y - t.x, 4f * WastelandLayout.TunnelFlare + 60f, "a real stretch of full tube");
                Assert.IsFalse(t.x < layout.GapEnd && t.y > layout.GapStart, "no tube over the bridge gap");
                Assert.IsFalse(t.x < layout.StartLine + 30f && t.y > layout.StartLine - 30f, "no tube over the grid");
                if (i > 0) Assert.Greater(t.x, layout.Tunnels[i - 1].y + 10f);
            }
            Assert.Greater(layout.Hairpin.x - layout.Tunnels[1].y, 90f, "room to brake for the hairpin after the west tube");
            Assert.AreEqual(0, WastelandLayout.StageFor(0));
            Assert.AreEqual(1, WastelandLayout.StageFor(1));
            Assert.AreEqual(2, WastelandLayout.StageFor(2));
            Assert.AreEqual(2, WastelandLayout.StageFor(3));

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
                foreach (var t in layout.Tunnels)
                {
                    float tunnel = (t.x + t.y) * 0.5f;
                    Assert.IsTrue(Physics.Raycast(path.Position(tunnel) + Vector3.up * 5f, Vector3.up,
                        out var ceiling, 10f, mask));
                    Assert.IsNotNull(ceiling.collider.GetComponent<GravitySurface>());
                }
                Assert.AreEqual(3, map.Pads.Count);

                // Rails stop cars until the leader's first lap; then the edges are marked, and after lap 2 they fall.
                float straight = layout.StartLine + 20f;
                bool Rail(float d) => Physics.Raycast(path.Position(d) + Vector3.up * 0.5f, path.Right(d), 12f, mask);
                Assert.IsTrue(Rail(straight), "rail up at the start");
                Assert.IsTrue(Deck(straight, 8.5f));
                map.SetStage(1);
                Assert.IsFalse(Rail(straight), "rails retracted after lap 1");
                Assert.IsTrue(Deck(straight, 8.5f), "edges still there during lap 2");
                Assert.AreEqual(WastelandLayout.HalfWidth, path.halfWidth);
                map.SetStage(2);
                Assert.AreEqual(WastelandLayout.NarrowHalfWidth, path.halfWidth, "AI and respawn keep to the narrow road");
                map.Tick(WastelandTrack.CollapseDelay * 0.5f);
                Assert.IsTrue(Deck(straight, 8.5f), "a moment of warning before the edges go");
                map.Tick(WastelandTrack.CollapseDelay * 0.5f + WastelandTrack.CollapseSpread + 0.1f);
                Physics.SyncTransforms();
                Assert.IsTrue(map.EdgesGone);
                foreach (float d in new[] { straight, layout.GapStart - 30f, layout.Hairpin.x + 10f })
                {
                    Assert.IsFalse(Deck(d, -8.5f), "left edge gone");
                    Assert.IsFalse(Deck(d, 8.5f), "right edge gone");
                    Assert.IsTrue(Deck(d, -6f) && Deck(d, 6f), "narrow road stays");
                }
                map.SetStage(1);
                Assert.AreEqual(2, map.Stage, "stages never go back");
                Assert.IsNotNull(path.RespawnGuard);
                Assert.LessOrEqual(layout.SafeRespawn(layout.GapStart + 2f), layout.GapStart - WastelandLayout.JumpRunUp);
                map.Restore();
                Assert.IsFalse(path.HasLayout);
                Assert.AreEqual(originalWidth, path.halfWidth, "narrowing is undone with the map");
            }
            finally
            {
                if (map != null) Object.DestroyImmediate(map.gameObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
