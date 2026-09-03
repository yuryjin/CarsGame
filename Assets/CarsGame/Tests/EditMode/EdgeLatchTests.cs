using NUnit.Framework;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    public class EdgeLatchTests
    {
        [Test]
        public void PressSurvivesUntilConsumedOnce()
        {
            var latch = new EdgeLatch();
            latch.Press();
            Assert.IsFalse(latch.Consume() && latch.Consume(), "second consume must be false");
        }

        [Test]
        public void ZeroTicksBetweenFramesKeepsThePress()
        {
            var latch = new EdgeLatch();
            latch.Press();
            // frame passes with no fixed tick, then a tick samples
            Assert.IsTrue(latch.HasPending);
            Assert.IsTrue(latch.Consume());
            Assert.IsFalse(latch.Consume());
        }

        [Test]
        public void SeveralTicksBetweenFramesFireOnce()
        {
            var latch = new EdgeLatch();
            latch.Press();
            int fired = 0;
            for (int tick = 0; tick < 5; tick++) if (latch.Consume()) fired++;
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void ConsumeAllCollapsesDoubleTaps()
        {
            var latch = new EdgeLatch();
            latch.Press(); latch.Press();
            Assert.IsTrue(latch.ConsumeAll());
            Assert.IsFalse(latch.ConsumeAll());
        }
    }
}
