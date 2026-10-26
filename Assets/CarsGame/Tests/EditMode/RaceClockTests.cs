using NUnit.Framework;
using RacingGame;

namespace RacingGame.Tests
{
    public class RaceClockTests
    {
        [SetUp]
        public void SetUp()
        {
            RaceClock.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            RaceClock.Reset();
        }

        [Test]
        public void ResetSetsTimeToZeroAndUnpauses()
        {
            RaceClock.Paused = true;
            RaceClock.Advance(0.1f);
            RaceClock.Reset();

            Assert.AreEqual(0d, RaceClock.Now);
            Assert.IsFalse(RaceClock.Paused);
        }

        [Test]
        public void AdvanceAccumulatesTime()
        {
            RaceClock.Advance(0.1f);
            RaceClock.Advance(0.05f);

            Assert.AreEqual(0.15d, RaceClock.Now, 1e-5d);
        }

        [Test]
        public void AdvanceClampsLargeStepsToMaxFrameStep()
        {
            RaceClock.Advance(RaceClock.MaxFrameStep + 1f);
            Assert.AreEqual((double)RaceClock.MaxFrameStep, RaceClock.Now, 1e-5d);
        }

        [Test]
        public void AdvanceIgnoresWhenPausedOrNonPositive()
        {
            RaceClock.Paused = true;
            RaceClock.Advance(0.1f);
            Assert.AreEqual(0d, RaceClock.Now);

            RaceClock.Paused = false;
            RaceClock.Advance(0f);
            RaceClock.Advance(-0.5f);
            Assert.AreEqual(0d, RaceClock.Now);
        }
    }
}
