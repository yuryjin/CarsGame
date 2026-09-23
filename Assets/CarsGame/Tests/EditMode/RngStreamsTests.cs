using NUnit.Framework;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    public class RngStreamsTests
    {
        [Test]
        public void SameSeedProducesIdenticalSequences()
        {
            var rng1 = new RngStreams(12345);
            var rng2 = new RngStreams(12345);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(rng1.Range(RngStreams.Stream.Ai, 0f, 10f), rng2.Range(RngStreams.Stream.Ai, 0f, 10f));
                Assert.AreEqual(rng1.Range(RngStreams.Stream.Gameplay, 1, 100), rng2.Range(RngStreams.Stream.Gameplay, 1, 100));
                Assert.AreEqual(rng1.Range(RngStreams.Stream.Spawn, -5f, 5f), rng2.Range(RngStreams.Stream.Spawn, -5f, 5f));
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentSequences()
        {
            var rng1 = new RngStreams(12345);
            var rng2 = new RngStreams(54321);

            float diffSum = 0f;
            for (int i = 0; i < 20; i++)
            {
                diffSum += System.Math.Abs(rng1.Range(RngStreams.Stream.Ai, 0f, 1f) - rng2.Range(RngStreams.Stream.Ai, 0f, 1f));
            }

            Assert.Greater(diffSum, 0.5f);
        }

        [Test]
        public void StreamsAreIndependent()
        {
            var rngA = new RngStreams(999);
            var rngB = new RngStreams(999);

            // Draw 100 numbers from AI stream on rngA
            for (int i = 0; i < 100; i++)
            {
                rngA.Get(RngStreams.Stream.Ai).Next();
            }

            // Gameplay and Spawn streams on rngA should still match rngB perfectly
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(rngA.Range(RngStreams.Stream.Gameplay, 0, 1000), rngB.Range(RngStreams.Stream.Gameplay, 0, 1000));
                Assert.AreEqual(rngA.Range(RngStreams.Stream.Spawn, 0f, 100f), rngB.Range(RngStreams.Stream.Spawn, 0f, 100f));
            }
        }

        [Test]
        public void RangeFloatStaysWithinBounds()
        {
            var rng = new RngStreams(42);
            float min = 10f;
            float max = 25f;

            for (int i = 0; i < 200; i++)
            {
                float val = rng.Range(RngStreams.Stream.Gameplay, min, max);
                Assert.GreaterOrEqual(val, min);
                Assert.Less(val, max);
            }
        }

        [Test]
        public void RangeIntStaysWithinBounds()
        {
            var rng = new RngStreams(42);
            int min = 5;
            int max = 15;

            for (int i = 0; i < 200; i++)
            {
                int val = rng.Range(RngStreams.Stream.Ai, min, max);
                Assert.GreaterOrEqual(val, min);
                Assert.Less(val, max);
            }
        }
    }
}
