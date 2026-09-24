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

        [Test]
        public void ArenaRankingOrdersByKillsDeathsAndDeterministicCarId()
        {
            var entries = new List<RankEntry>
            {
                new RankEntry { CarId = 5, Kills = 2, Deaths = 1 },
                new RankEntry { CarId = 2, Kills = 4, Deaths = 3 },
                new RankEntry { CarId = 3, Kills = 2, Deaths = 0 },
                new RankEntry { CarId = 1, Kills = 4, Deaths = 1 },
                new RankEntry { CarId = 4, Kills = 2, Deaths = 1 }
            };

            // Expected order:
            // 1st: Car 1 (4 kills, 1 death)
            // 2nd: Car 2 (4 kills, 3 deaths)
            // 3rd: Car 3 (2 kills, 0 deaths)
            // 4th: Car 4 (2 kills, 1 death, CarId 4 < 5)
            // 5th: Car 5 (2 kills, 1 death, CarId 5 > 4)
            var pos = RaceRanking.RankArena(entries);
            Assert.AreEqual(5, pos[0]); // Car 5 is 5th
            Assert.AreEqual(2, pos[1]); // Car 2 is 2nd
            Assert.AreEqual(3, pos[2]); // Car 3 is 3rd
            Assert.AreEqual(1, pos[3]); // Car 1 is 1st
            Assert.AreEqual(4, pos[4]); // Car 4 is 4th
        }

        [Test]
        public void ArenaBufferedRankMatchesAllocatingRankAndDoesNotAllocate()
        {
            var entries = new List<RankEntry>
            {
                new RankEntry { CarId = 1, Kills = 3, Deaths = 1 },
                new RankEntry { CarId = 2, Kills = 0, Deaths = 2 },
                new RankEntry { CarId = 3, Kills = 5, Deaths = 0 },
                new RankEntry { CarId = 4, Kills = 3, Deaths = 0 },
                new RankEntry { CarId = 5, Kills = 1, Deaths = 1 }
            };
            var expected = RaceRanking.RankArena(entries);
            var order = new List<int>(8);
            var positions = new int[8];
            for (int i = 0; i < 200; i++) RaceRanking.RankArena(entries, order, positions);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) RaceRanking.RankArena(entries, order, positions);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            for (int i = 0; i < entries.Count; i++) Assert.AreEqual(expected[i], positions[i], $"entry {i}");
            Assert.AreEqual(0, allocated, "RankArena with caller buffers must not allocate");
        }
    }
}
