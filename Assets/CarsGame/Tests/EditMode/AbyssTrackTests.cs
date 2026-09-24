using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public sealed class AbyssTrackTests
    {
        [Test]
        public void LapsChangeActualCollidersAndRestoreWidth()
        {
            var root = new GameObject("TestPath");
            AbyssTrack map = null;
            try
            {
                for (int i = 0; i < 128; i++)
                {
                    var point = new GameObject("Point").transform;
                    point.SetParent(root.transform);
                    float angle = i * Mathf.PI * 2f / 128;
                    point.position = new Vector3(Mathf.Sin(angle) * 100f, 0f, Mathf.Cos(angle) * 100f);
                }
                var path = root.AddComponent<TrackPath>();
                float original = path.halfWidth;
                map = AbyssTrack.Create(path);
                int mask = 1 << LayerMask.NameToLayer("Track");
                bool Floor(float lane)
                {
                    Physics.SyncTransforms();
                    return Physics.Raycast(path.Position(50f) + path.Right(50f) * lane + Vector3.up * 4f,
                        Vector3.down, 6f, mask);
                }
                bool Wall(float distance, float side)
                {
                    Physics.SyncTransforms();
                    return Physics.Raycast(path.Position(distance) + Vector3.up * 0.6f,
                        path.Right(distance) * side, 6f, mask);
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    Assert.IsTrue(Floor(side * 4f), "wide road");
                    Assert.IsFalse(Floor(side * 6f), "abyss begins at road edge");
                    Assert.IsTrue(Wall(4f, side), "lap one barrier");
                    Assert.IsFalse(Wall(10f, side), "gap between barriers");
                }
                map.SetLap(2);
                Assert.IsTrue(Floor(4f));
                Assert.IsFalse(Wall(4f, 1f));
                Assert.IsFalse(Wall(4f, -1f));
                map.SetLap(3);
                Assert.AreEqual(AbyssTrack.NarrowHalfWidth, path.halfWidth);
                foreach (float side in new[] { -1f, 1f })
                {
                    Assert.IsFalse(Floor(side * 4f), "shoulder collider removed");
                    Assert.IsTrue(Floor(side * 2f), "centre survives");
                    Assert.IsFalse(Wall(4f, side));
                }
                map.Restore();
                Assert.AreEqual(original, path.halfWidth);
                Assert.IsFalse(Floor(0f), "no leftover map colliders after teardown");
            }
            finally
            {
                if (map != null) Object.DestroyImmediate(map.gameObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
