using System.Collections.Generic;
using NUnit.Framework;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    public class RaceRankingTests
    {
        private static RankEntry Finished(int id, double t) => new RankEntry { CarId = id, Finished = true, FinishTime = t };
        private static RankEntry Racing(int id, double progress) => new RankEntry { CarId = id, TotalProgress = progress };

        [Test]
        public void FinishedCarsPrecedeRacingCarsRegardlessOfProgress()
        {
            var entries = new List<RankEntry> { Racing(1, 5000), Finished(2, 300), Racing(3, -20) };
            var pos = RaceRanking.Rank(entries);
            Assert.AreEqual(1, pos[1]);
            Assert.AreEqual(2, pos[0]);
            Assert.AreEqual(3, pos[2]);
        }

        [Test]
        public void FasterFinisherWins()
        {
            var entries = new List<RankEntry> { Finished(1, 120.5), Finished(2, 99.0) };
            var pos = RaceRanking.Rank(entries);
            Assert.AreEqual(2, pos[0]);
            Assert.AreEqual(1, pos[1]);
        }

        [Test]
        public void RacingCarsOrderedByProgressDescending()
        {
            var entries = new List<RankEntry> { Racing(1, 100), Racing(2, 940), Racing(3, 500) };
            var pos = RaceRanking.Rank(entries);
            Assert.AreEqual(3, pos[0]);
            Assert.AreEqual(1, pos[1]);
            Assert.AreEqual(2, pos[2]);
        }

        [Test]
        public void TiesBreakByCarIdAndOrderIsTotal()
        {
            var a = Racing(7, 100); var b = Racing(3, 100);
            Assert.Less(RaceRanking.Instance.Compare(b, a), 0);
            Assert.Greater(RaceRanking.Instance.Compare(a, b), 0);
            Assert.AreEqual(0, RaceRanking.Instance.Compare(a, a));
        }
    

        [Test]
        public void BufferedRankMatchesAllocatingRankAndDoesNotAllocate()
        {
            var entries = new List<RankEntry> { Racing(4, 300), Finished(2, 95.5), Racing(1, 300), Racing(3, 900), Finished(5, 90.1), Racing(6, -10) };
            var expected = RaceRanking.Rank(entries);
            var order = new List<int>(8);
            var positions = new int[8];
            for (int i = 0; i < 200; i++) RaceRanking.Rank(entries, order, positions);   // JIT/tiering before measuring
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) RaceRanking.Rank(entries, order, positions);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            for (int i = 0; i < entries.Count; i++) Assert.AreEqual(expected[i], positions[i], $"entry {i}");
            Assert.AreEqual(new[] { 5, 2, 4, 3, 1, 6 }, expected);
            Assert.AreEqual(0, allocated, "ranking with caller buffers must not allocate");
        }
    }
}
