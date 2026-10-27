using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public sealed class StarRingTests
    {
        private static float Radius(Vector3[] p, int i)
        {
            int n = p.Length;
            Vector3 a = p[(i - 2 + n) % n], b = p[i], c = p[(i + 2) % n];
            Vector2 u = new Vector2(b.x - a.x, b.z - a.z), v = new Vector2(c.x - b.x, c.z - b.z);
            float turn = Mathf.Abs(Vector2.SignedAngle(u, v)) * Mathf.Deg2Rad;
            return turn < 1e-4f ? float.PositiveInfinity : (u.magnitude + v.magnitude) * 0.5f / turn;
        }

        [Test]
        public void LayoutClosesWithoutCrossingItself()
        {
            var layout = new StarRingLayout();
            var p = layout.Points;
            Assert.That(layout.Length, Is.InRange(1000f, 1600f));
            Assert.Less(Vector3.Distance(p[p.Length - 1], p[0]), StarRingLayout.Step * 1.5f, "the last chord ends at the start");
            var spline = new TrackSpline(p);
            Assert.AreEqual(layout.Length, spline.Length, 0.5f, "feature distances index the spline");
            // Parts of the loop far apart along it are far apart in space too: no crossings, no ambiguous projection.
            float nearest = float.MaxValue;
            for (int i = 0; i < p.Length; i += 2)
                for (int j = i + 1; j < p.Length; j += 2)
                {
                    float along = Mathf.Abs(i - j) * StarRingLayout.Step;
                    if (Mathf.Min(along, layout.Length - along) < 150f) continue;
                    nearest = Mathf.Min(nearest, new Vector2(p[i].x - p[j].x, p[i].z - p[j].z).magnitude);
                }
            Assert.Greater(nearest, 60f);
        }

        [Test]
        public void OnlyTheHairpinIsTight()
        {
            var layout = new StarRingLayout();
            var p = layout.Points;
            float tightest = float.MaxValue, tightestElsewhere = float.MaxValue, at = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                float r = Radius(p, i), d = i * layout.Length / p.Length;
                if (r < tightest) { tightest = r; at = d; }
                if (d < layout.Hairpin.x - 6f || d > layout.Hairpin.y + 6f) tightestElsewhere = Mathf.Min(tightestElsewhere, r);
            }
            Assert.AreEqual(StarRingLayout.HairpinRadius, tightest, 3f, "one semi-sharp corner");
            Assert.That(at, Is.InRange(layout.Hairpin.x, layout.Hairpin.y));
            Assert.GreaterOrEqual(tightestElsewhere, 65f, "every other bend is a gentle sweeper");
        }

        // Point-mass flight off the kicker: clear at a decent pace, fall when too slow, never past the landing deck.
        private static float Landing(float kmh, StarRingLayout layout)
        {
            float v = kmh / 3.6f, angle = Mathf.Atan(StarRingLayout.KickSlope);
            float vx = v * Mathf.Cos(angle), vy = v * Mathf.Sin(angle), y = StarRingLayout.LipHeight, x = 0f;
            for (float t = 0f; t < 5f; t += 0.001f)
            {
                x += vx * 0.001f; vy -= 9.81f * 0.001f; y += vy * 0.001f;
                if (y > StarRingLayout.LandingHeight) continue;
                return x >= StarRingLayout.GapLength ? x : -1f;   // under the landing deck before its edge: fell
            }
            return -1f;
        }

        [Test]
        public void TheGapNeedsSpeed()
        {
            var layout = new StarRingLayout();
            Assert.Less(Landing(55f, layout), 0f, "55 km/h falls into the gap");
            Assert.Greater(Landing(75f, layout), StarRingLayout.GapLength, "75 km/h clears it");
            float boosted = Landing(160f, layout);   // faster than a boosted standard car
            Assert.Greater(boosted, 0f);
            Assert.Less(layout.GapStart + boosted, layout.Range("drop").x + 20f, "even a boosted car lands on the high road");
            Assert.AreEqual(StarRingLayout.DeckHeight, layout.Height(layout.GapStart - StarRingLayout.KickLength - 1f), 1e-3f);
            Assert.AreEqual(StarRingLayout.LipHeight, layout.Height(layout.GapStart - 1e-3f), 0.01f);
            for (float d = 0f; d < layout.Length; d += 0.5f)
                if (!layout.InGap(d) && !layout.InGap(d + 0.5f))
                    Assert.Less(Mathf.Abs(layout.Height(d + 0.5f) - layout.Height(d)), 0.1f, $"smooth road at {d}");
        }

        [Test]
        public void RespawnNeverLandsInTheGapAndLeavesARunUp()
        {
            var layout = new StarRingLayout();
            for (float d = layout.GapStart - 60f; d < layout.GapEnd + 2f; d += 1f)
                Assert.LessOrEqual(layout.SafeRespawn(d), layout.GapStart - StarRingLayout.JumpRunUp, $"from {d}");
            Assert.AreEqual(layout.Hairpin.x, layout.SafeRespawn(layout.Hairpin.x));
            Assert.AreEqual(10f, layout.SafeRespawn(10f));
            Assert.Greater(layout.GapStart - StarRingLayout.JumpRunUp, layout.Range("rise").x, "the run-up starts on the climb, not after the corner");
        }

        [Test]
        public void GeometryHasDeckGapTubeRailsAndRestoresThePath()
        {
            var root = new GameObject("TestPath");
            for (int i = 0; i < 64; i++)
            {
                var point = new GameObject("Point").transform;
                point.SetParent(root.transform);
                float angle = i * Mathf.PI * 2f / 64;
                point.position = new Vector3(Mathf.Sin(angle) * 80f, 0f, Mathf.Cos(angle) * 80f);
            }
            var path = root.AddComponent<TrackPath>();
            float width = path.halfWidth, authoredLength = path.Length;
            StarRingTrack map = null;
            try
            {
                map = StarRingTrack.Create(path, null);
                var layout = map.Layout;
                Physics.SyncTransforms();
                Assert.AreEqual(layout.Length, path.Length, 0.5f);
                Assert.AreEqual(StarRingLayout.HalfWidth, path.halfWidth);
                int mask = 1 << LayerMask.NameToLayer("Track");
                // The path points carry the road height already.
                Vector3 Surface(float d, float lane) => path.Position(d) + path.Right(d) * lane;
                bool Down(float d, float lane) => Physics.Raycast(Surface(d, lane) + Vector3.up * 3f, Vector3.down, 6f, mask);

                foreach (float lane in new[] { -7f, 0f, 7f })
                {
                    Assert.IsTrue(Down(layout.StartLine, lane), "deck at the start");
                    Assert.IsTrue(Down(layout.GapStart - 2f, lane), "deck before the lip");
                    Assert.IsFalse(Down((layout.GapStart + layout.GapEnd) * 0.5f, lane), "open gap");
                    Assert.IsTrue(Down(layout.GapEnd + 2f, lane), "landing deck");
                }

                // Mid-tunnel: a closed tube that is a GravitySurface all round.
                float mid = (layout.TunnelStart + layout.TunnelEnd) * 0.5f;
                Vector3 inside = Surface(mid, 0f) + Vector3.up * 5f;
                Assert.IsTrue(Physics.Raycast(inside, Vector3.up, out var ceiling, 10f, mask));
                Assert.AreEqual(StarRingTrack.TubeHeight - 5f, ceiling.distance, 0.2f);
                Assert.Less(Vector3.Dot(ceiling.normal, Vector3.up), -0.95f, "ceiling faces down, into the tube");
                Assert.IsNotNull(ceiling.collider.GetComponent<GravitySurface>());
                Assert.IsTrue(Physics.Raycast(inside, path.Right(mid), out var wall, 12f, mask));
                Assert.AreEqual(StarRingLayout.HalfWidth, wall.distance, 0.3f);
                Assert.IsNotNull(wall.collider.GetComponent<GravitySurface>());
                Assert.IsFalse(Physics.Raycast(Surface(layout.StartLine, 0f) + Vector3.up * 3f, Vector3.up, 30f, mask), "open sky outside the tunnel");

                // Rails on the straights, none on the outside of the semi-sharp corner.
                bool Side(float d, float sign) => Physics.Raycast(Surface(d, 0f) + Vector3.up * 0.5f, path.Right(d) * sign, StarRingLayout.HalfWidth + 1f, mask);
                Assert.IsTrue(Side(layout.StartLine + 20f, -1f) && Side(layout.StartLine + 20f, 1f));
                float apex = (layout.Hairpin.x + layout.Hairpin.y) * 0.5f;
                Assert.IsFalse(Side(apex, StarRingLayout.HairpinOutside), "open edge outside the corner");
                Assert.IsTrue(Side(apex, -StarRingLayout.HairpinOutside), "inside rail stays");

                Assert.AreEqual(5, map.Turrets.Count);
                foreach (var turret in map.Turrets)
                {
                    int hint = -1;
                    float d = path.Project(turret.transform.position, ref hint);
                    Assert.Greater(Mathf.Abs(path.Spline.LateralOffset(turret.transform.position, d)), StarRingLayout.HalfWidth + RailClearance, "turrets stand beside the road");
                }
                Assert.AreEqual(2, map.Pads.Count);
                Assert.AreEqual(1, System.Array.FindAll(new[] { map.Pads[0], map.Pads[1] }, g => g.transform.up.y < -0.9f).Length, "one pad on the tunnel ceiling");
                Assert.IsNotNull(path.RespawnGuard);
                Assert.IsNotNull(path.Lures);

                map.SetStage(2);
                Assert.IsFalse(Side(layout.StartLine + 20f, 1f), "retracted rails cannot trap a car");
                Assert.IsTrue(Down(layout.StartLine, 7f), "lap two retains the wide deck");
                Assert.IsTrue(map.transform.Find("NarrowEdge").gameObject.activeSelf, "surviving corridor is marked early");
                map.Tick(StarRingTrack.RailSinkSeconds / 2f);
                Assert.Less(map.transform.Find("Rails").localPosition.y, 0f, "rails visibly retract");
                map.Tick(StarRingTrack.RailSinkSeconds);
                Assert.IsFalse(map.transform.Find("Rails").gameObject.activeSelf);

                map.SetStage(3);
                Assert.AreEqual(StarRingTrack.NarrowHalfWidth, path.halfWidth, "AI and respawn leave the edges early");
                map.Tick(StarRingTrack.CollapseDelay - 0.1f);
                Assert.IsTrue(Down(layout.StartLine, 7f), "the whole warning interval is safe");
                map.SetStage(3); // Another racer crossing must not restart the warning.
                map.SetStage(2);
                map.Tick(0.2f);
                Assert.IsTrue(map.EdgesGone);
                Physics.SyncTransforms();
                foreach (float d in new[] { layout.StartLine, layout.GapStart - 2f, layout.GapEnd + 2f })
                {
                    Assert.IsTrue(Down(d, 0f), "core survives the collapse");
                    Assert.IsFalse(Down(d, -7f) || Down(d, 7f), "both shoulders lose support");
                }
                Assert.IsTrue(Physics.Raycast(inside, Vector3.up, out ceiling, 10f, mask), "tunnel remains intact");
                map.Tick(StarRingTrack.CollapseSeconds);
                Assert.IsFalse(map.transform.Find("Shoulders").gameObject.activeSelf);
                Assert.IsTrue(map.transform.Find("NarrowEdge").gameObject.activeSelf);

                // No finish/jump paint or bridge end-cap may suggest a surviving outer lane.
                foreach (var filter in map.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(vertex);
                    int hint = -1;
                    float d = path.Project(world, ref hint);
                    bool finish = Mathf.Abs(d - layout.StartLine) < 0.8f;
                    bool lip = Mathf.Abs(d - layout.GapStart) < 2f;
                    bool landing = Mathf.Abs(d - layout.GapEnd) < 2f;
                    float y = world.y - layout.Height(d);
                    if (!(finish || lip || landing) || y < -StarRingTrack.SkinDepth - 0.1f || y > 0.1f) continue;
                    Assert.LessOrEqual(Mathf.Abs(path.Spline.LateralOffset(world, d)), StarRingTrack.NarrowHalfWidth + 0.1f,
                        $"{filter.name}: surface decoration must not hang over removed shoulders at {d:F1}");
                }
            }
            finally
            {
                // Edit mode never calls OnDestroy: restore explicitly, as the flow's teardown does.
                if (map != null) { map.Restore(); Object.DestroyImmediate(map.gameObject); }
                Assert.IsFalse(path.HasLayout);
                Assert.AreEqual(width, path.halfWidth);
                Assert.AreEqual(authoredLength, path.Length, 0.01f);
                Assert.IsNull(path.RespawnGuard);
                Object.DestroyImmediate(root);
                var driver = SimulationDriver.Current;
                if (driver != null) Object.DestroyImmediate(driver.gameObject);
            }
        }

        private const float RailClearance = 2f;
    }
}
