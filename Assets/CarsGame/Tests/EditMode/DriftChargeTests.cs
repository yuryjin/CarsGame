using NUnit.Framework;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    public class DriftChargeTests
    {
        private const float Dt = 0.02f;

        private static float Run(DriftCharge d, bool brake, float steer, float speed, float seconds, bool grounded = true)
        {
            float boost = 0f;
            for (int i = 0; i < Mathf(seconds); i++) boost += d.Step(brake, steer, speed, grounded, Dt);
            return boost;
        }
        private static int Mathf(float seconds) => (int)System.Math.Round(seconds / Dt);

        [Test]
        public void BrakeAndSteerAtSpeedStartsADrift()
        {
            var d = new DriftCharge();
            Run(d, true, 0.8f, 20f, 0.1f);
            Assert.IsTrue(d.Drifting); Assert.AreEqual(1, d.Direction);
        }

        [Test]
        public void SlowOrStraightBrakingIsJustBraking()
        {
            var slow = new DriftCharge(); Run(slow, true, 1f, 8f, 0.5f); Assert.IsFalse(slow.Drifting);
            var straight = new DriftCharge(); Run(straight, true, 0f, 25f, 0.5f); Assert.IsFalse(straight.Drifting);
        }

        [Test]
        public void SteerCanFollowTheBrakeWithinTheWindowButNotLater()
        {
            var quick = new DriftCharge(); Run(quick, true, 0f, 20f, 0.1f); Run(quick, true, 0.7f, 20f, 0.04f);
            Assert.IsTrue(quick.Drifting);
            var late = new DriftCharge(); Run(late, true, 0f, 20f, 0.6f); Run(late, true, 0.7f, 20f, 0.1f);
            Assert.IsFalse(late.Drifting, "a long hard stop that turns into a corner stays a stop");
        }

        [Test]
        public void ReleaseFiresABoostByStage()
        {
            var d = new DriftCharge();
            Run(d, true, -1f, 20f, 0.3f);
            Assert.AreEqual(0f, Run(d, false, -1f, 20f, 0.02f), 1e-6f, "too short: no stage");
            Assert.IsFalse(d.Drifting);

            var full = new DriftCharge();
            Run(full, true, 1f, 20f, 2.0f);
            Assert.AreEqual(3, full.Stage);
            Assert.AreEqual(full.StageBoost[2], Run(full, false, 1f, 20f, 0.02f), 1e-6f);

            var mid = new DriftCharge();
            Run(mid, true, 1f, 20f, 0.7f);
            Assert.AreEqual(1, mid.Stage);
        }

        [Test]
        public void LosingSpeedOrCounterSteeringCancelsTheCharge()
        {
            var d = new DriftCharge();
            Run(d, true, 1f, 20f, 1.5f);
            Run(d, true, 1f, 5f, 0.02f);
            Assert.IsFalse(d.Drifting);
            Assert.AreEqual(0f, Run(d, false, 1f, 5f, 0.1f), 1e-6f);

            var c = new DriftCharge();
            Run(c, true, 1f, 20f, 1.5f);
            Run(c, true, -1f, 20f, 0.02f);
            Assert.IsFalse(c.Drifting);
        }

        [Test]
        public void ShortHopsKeepTheDriftLongAirtimeEndsIt()
        {
            var d = new DriftCharge();
            Run(d, true, 1f, 20f, 0.5f);
            Run(d, true, 1f, 20f, 0.2f, grounded: false);
            Assert.IsTrue(d.Drifting);
            Run(d, true, 1f, 20f, 0.5f, grounded: false);
            Assert.IsFalse(d.Drifting);
        }
    }
}
