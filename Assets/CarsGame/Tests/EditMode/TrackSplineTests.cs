using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public class TrackSplineTests
    {
        private static TrackSpline Square(float side = 100f) => new TrackSpline(new[]
        {
            new Vector3(0, 0, 0), new Vector3(side, 0, 0), new Vector3(side, 0, side), new Vector3(0, 0, side)
        });

        [Test]
        public void LengthIsPerimeter()
        {
            Assert.AreEqual(400f, Square().Length, 1e-3f);
        }

        [Test]
        public void ProjectRoundTripsPosition()
        {
            var s = Square();
            int hint = -1;
            foreach (float d in new[] { 0f, 37.5f, 100f, 150f, 250f, 399f })
            {
                Vector3 p = s.Position(d);
                float back = s.Project(p, ref hint);
                Assert.AreEqual(d, back, 0.01f, $"d={d}");
            }
        }

        [Test]
        public void ProjectWithHintFindsNearbySegment()
        {
            var s = Square();
            int hint = -1;
            s.Project(new Vector3(50, 0, -3), ref hint);      // on segment 0
            float d = s.Project(new Vector3(103, 0, 20), ref hint, 1); // segment 1, offset right
            Assert.AreEqual(120f, d, 0.01f);
        }

        [Test]
        public void WrapAndSignedDelta()
        {
            var s = Square();
            Assert.AreEqual(10f, s.Wrap(410f), 1e-3f);
            Assert.AreEqual(390f, s.Wrap(-10f), 1e-3f);
            Assert.AreEqual(20f, s.SignedDelta(390f, 10f), 1e-3f);
            Assert.AreEqual(-20f, s.SignedDelta(10f, 390f), 1e-3f);
        }

        [Test]
        public void ReversedKeepsLength()
        {
            Assert.AreEqual(Square().Length, Square().Reversed().Length, 1e-3f);
        }

        [Test]
        public void LateralOffsetIsSignedRight()
        {
            var s = Square();
            // segment 0 runs +X, so "right" is -Z
            Assert.Greater(s.LateralOffset(new Vector3(50, 0, -2), 50f), 1f);
            Assert.Less(s.LateralOffset(new Vector3(50, 0, 2), 50f), -1f);
        }

        [Test]
        public void CurvatureIsZeroOnStraightAndPositiveBeforeCorner()
        {
            var s = Square();
            Assert.Less(s.CurvatureAhead(20f, 20f), 0.5f);
            Assert.Greater(s.CurvatureAhead(85f, 30f), 0.5f);
        }
    }
}
