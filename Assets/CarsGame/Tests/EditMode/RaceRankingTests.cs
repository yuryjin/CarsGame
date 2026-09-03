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
    }
}
