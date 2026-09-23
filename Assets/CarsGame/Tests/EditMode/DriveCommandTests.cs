using NUnit.Framework;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    public class DriveCommandTests
    {
        [Test]
        public void NoneCreatesDefaultCommandForTick()
        {
            var cmd = DriveCommand.None(42);
            Assert.AreEqual(42, cmd.Tick);
            Assert.AreEqual(0f, cmd.Throttle);
            Assert.AreEqual(0f, cmd.Steer);
            Assert.IsFalse(cmd.Brake);
            Assert.IsFalse(cmd.Fire);
            Assert.IsFalse(cmd.Secondary);
            Assert.IsFalse(cmd.UseItem);
            Assert.IsFalse(cmd.ResetEdge);
        }

        [Test]
        public void ClampedClampsThrottleAndSteer()
        {
            var cmd = new DriveCommand
            {
                Tick = 10,
                Throttle = 2.5f,
                Steer = -3.0f,
                Brake = true,
                Fire = true,
                ResetEdge = true
            };

            var clamped = cmd.Clamped();
            Assert.AreEqual(10, clamped.Tick);
            Assert.AreEqual(1f, clamped.Throttle);
            Assert.AreEqual(-1f, clamped.Steer);
            Assert.IsTrue(clamped.Brake);
            Assert.IsTrue(clamped.Fire);
            Assert.IsTrue(clamped.ResetEdge);

            cmd.Throttle = -5f;
            cmd.Steer = 10f;
            clamped = cmd.Clamped();
            Assert.AreEqual(-1f, clamped.Throttle);
            Assert.AreEqual(1f, clamped.Steer);
        }

        [Test]
        public void ClampedPreservesInBoundValues()
        {
            var cmd = new DriveCommand
            {
                Tick = 5,
                Throttle = 0.75f,
                Steer = -0.3f
            };

            var clamped = cmd.Clamped();
            Assert.AreEqual(0.75f, clamped.Throttle);
            Assert.AreEqual(-0.3f, clamped.Steer);
        }
    }
}
