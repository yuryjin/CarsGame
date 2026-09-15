using NUnit.Framework;
using RacingGame.Core.Economy;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    public class KillTallyTests
    {
        [Test]
        public void CountsPerVictimInFirstKillOrder()
        {
            var t = new KillTally(2);
            t.Record(3); t.Record(5); t.Record(3); t.Record(9);   // grows past the initial capacity
            Assert.AreEqual(4, t.Total);
            Assert.AreEqual(3, t.DistinctVictims);
            Assert.AreEqual(2, t.CountFor(3));
            Assert.AreEqual(0, t.CountFor(7));
            Assert.AreEqual(new[] { 2, 1, 1 }, t.ToPerVictimCounts());
            t.Clear();
            Assert.AreEqual(0, t.Total);
            Assert.IsNull(t.ToPerVictimCounts());
        }

        [Test]
        public void ThreeDifferentVictimsPayTheFullBonusEach()
        {
            var t = new KillTally();
            t.Record(1); t.Record(2); t.Record(3);
            int baseReward = Economy.RaceReward(4, true, null, 1f);
            int withKills = Economy.RaceReward(4, true, t.ToPerVictimCounts(), 1f);
            Assert.AreEqual(3 * Economy.KillBonus, withKills - baseReward, "75 coins, not the 43 of a single-victim ladder");
        }

        [Test]
        public void RecordingDoesNotAllocateWithinCapacity()
        {
            var warm = new KillTally(8);
            for (int i = 0; i < 200; i++) warm.Record(1 + i % 7);   // JIT/tiering before measuring
            var t = new KillTally(8);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++) t.Record(1 + i % 7);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, $"allocated {allocated} bytes");
        }
    }
}
