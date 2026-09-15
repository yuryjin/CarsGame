using NUnit.Framework;
using RacingGame.Core.Track;
using UnityEngine;

namespace RacingGame.Tests
{
    public class TrackProfileTests
    {
        private static TrackSpline Circle(float radius, int points, bool clockwise)
        {
            var p = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float a = (clockwise ? -1f : 1f) * i * Mathf.PI * 2f / points;
                p[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            return new TrackSpline(p);
        }

        // Straight 200 m, U-turn of radius 20, straight back, U-turn: a stadium loop.
        private static TrackSpline Stadium()
        {
            var list = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i <= 20; i++) list.Add(new Vector3(0f, 0f, i * 10f));
            for (int i = 1; i < 12; i++) { float a = Mathf.PI * i / 12f; list.Add(new Vector3(20f - Mathf.Cos(a) * 20f, 0f, 200f + Mathf.Sin(a) * 20f)); }
            for (int i = 20; i >= 0; i--) list.Add(new Vector3(40f, 0f, i * 10f));
            for (int i = 1; i < 12; i++) { float a = Mathf.PI * i / 12f; list.Add(new Vector3(20f + Mathf.Cos(a) * 20f, 0f, -Mathf.Sin(a) * 20f)); }
            return new TrackSpline(list.ToArray());
        }

        [Test]
        public void CircleHasConstantCurvatureSpeedAndInsideLane()
        {
            var s = TrackProfile.Settings.Default;
            s.topSpeed = 100f;
            float radius = 50f;
            var cw = new TrackProfile(Circle(radius, 90, true), s);
            float expectedCurv = Mathf.Rad2Deg / radius;           // deg per metre
            float expectedSpeed = Mathf.Sqrt(s.lateralGrip * radius);
            for (float d = 0f; d < cw.Length; d += 17f)
            {
                Assert.AreEqual(expectedCurv, Mathf.Abs(cw.CurvatureAt(d)), 0.05f, $"curvature at {d}");
                Assert.AreEqual(expectedSpeed, cw.SpeedAt(d), 0.6f, $"speed at {d}");
            }
            var ccw = new TrackProfile(Circle(radius, 90, false), s);
            Assert.AreEqual(-Mathf.Sign(cw.CurvatureAt(10f)), Mathf.Sign(ccw.CurvatureAt(10f)), "direction flips the sign");
            Assert.AreEqual(Mathf.Sign(cw.CurvatureAt(10f)), Mathf.Sign(cw.LaneAt(10f)), "racing line sits on the inside");
            Assert.LessOrEqual(Mathf.Abs(cw.LaneAt(10f)), s.laneLimit + 1e-4f);
        }

        [Test]
        public void ClockwiseLoopIsARightHandTurn()
        {
            // Unity: x right, z forward. Going +z then turning towards +x is a right turn.
            Assert.Greater(TrackProfile.SignedTurn(Vector3.forward, Vector3.right), 0f);
            Assert.Less(TrackProfile.SignedTurn(Vector3.forward, Vector3.left), 0f);
        }

        [Test]
        public void SpeedDropsBeforeTheCornerWithinBrakingLimits()
        {
            var s = TrackProfile.Settings.Default;
            s.topSpeed = 35f;
            var p = new TrackProfile(Stadium(), s);
            float cornerSpeed = Mathf.Sqrt(s.lateralGrip * 20f);
            // Middle of the first U-turn (straight is 200 m, half the turn ≈ 31 m).
            Assert.AreEqual(cornerSpeed, p.SpeedAt(231f), 1.5f, "corner speed");
            Assert.AreEqual(s.topSpeed, p.SpeedAt(60f), 0.01f, "early straight is flat out");
            Assert.Less(p.SpeedAt(195f), s.topSpeed, "braking starts before the corner");
            // No sample may exceed what braking at brakeDecel allows from the next one.
            float step = p.Length / p.SampleCount;
            for (int i = 0; i < p.SampleCount; i++)
            {
                float v = p.SpeedAt(i * step), next = p.SpeedAt((i + 1) * step);
                Assert.LessOrEqual(v * v, next * next + 2f * s.brakeDecel * step + 0.05f, $"sample {i}");
            }
            Assert.AreEqual(cornerSpeed, p.MinSpeedAhead(150f, 120f), 1.5f, "look-ahead finds the corner");
        }
    }
}
