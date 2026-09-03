using System.Collections.Generic;
using NUnit.Framework;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class SaveMergeTests
    {
        private static SaveData Make(long updated, string device, long earned, long spent, params string[] unlocked)
        {
            var d = new SaveData { updatedAtUnixMs = updated, deviceId = device };
            var row = d.Local(device); row.coinsEarned = earned; row.coinsSpent = spent;
            d.progress.unlockedEvents = new List<string>(unlocked);
            return d;
        }
        private static SaveData Make(long updated, long earned, long spent, params string[] unlocked) => Make(updated, "A", earned, spent, unlocked);

        [Test]
        public void SpentCoinsNeverResurrect()
        {
            var deviceA = Make(100, "A", 500, 400);   // spent on A
            var deviceB = Make(200, "A", 500, 0);     // stale copy of A's row, newer timestamp
            var m = SaveMerge.Merge(deviceA, deviceB);
            Assert.AreEqual(500, m.CoinsEarned);
            Assert.AreEqual(400, m.CoinsSpent);
            Assert.AreEqual(100, m.Coins);
        }

        [Test]
        public void ConcurrentSpendingOnTwoDevicesKeepsBothPurchases()
        {
            // Both devices start from the same synced state (A earned 1000) and spend independently.
            var onA = Make(100, "A", 1000, 400);
            var onB = Make(120, "A", 1000, 0); var rowB = onB.Local("B"); rowB.coinsSpent = 300;
            var m = SaveMerge.Merge(onA, onB);
            Assert.AreEqual(1000, m.CoinsEarned);
            Assert.AreEqual(700, m.CoinsSpent, "400 on A + 300 on B");
            Assert.AreEqual(300, m.Coins);
        }

        [Test]
        public void ConcurrentEarningsAndStatsSumAcrossDevices()
        {
            var a = Make(1, "A", 300, 0); a.Local("A").races = 4;
            var b = Make(2, "B", 250, 0); b.Local("B").races = 2;
            var m = SaveMerge.Merge(a, b);
            Assert.AreEqual(550, m.CoinsEarned);
            Assert.AreEqual(6, m.Races);
            Assert.AreEqual(2, m.progress.ledger.Count);
        }

        [Test]
        public void UnlocksUnionBestsMinUpgradesMax()
        {
            var a = Make(1, 0, 0, "e1", "e2"); a.progress.bests.Add(new EventBest { eventId = "e1", bestTimeMs = 60000 }); a.progress.upgrades = new[] { 2, 0, 1 };
            var b = Make(2, 0, 0, "e3"); b.progress.bests.Add(new EventBest { eventId = "e1", bestTimeMs = 58000 }); b.progress.upgrades = new[] { 1, 3, 1 };
            var m = SaveMerge.Merge(a, b);
            CollectionAssert.AreEquivalent(new[] { "e1", "e2", "e3" }, m.progress.unlockedEvents);
            Assert.AreEqual(58000, m.progress.bests.Find(x => x.eventId == "e1").bestTimeMs);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, m.progress.upgrades);
        }

        [Test]
        public void SettingsFollowTheNewerBlob()
        {
            var a = Make(100, 0, 0); a.settings.lang = "ru";
            var b = Make(200, 0, 0); b.settings.lang = "tr";
            Assert.AreEqual("tr", SaveMerge.Merge(a, b).settings.lang);
            Assert.AreEqual("tr", SaveMerge.Merge(b, a).settings.lang);
        }

        [Test]
        public void MigratorRoundTripsAndRejectsGarbage()
        {
            var d = Make(5, 10, 3, "e1");
            var back = SaveMigrator.FromJson(SaveMigrator.ToJson(d));
            Assert.AreEqual(7, back.Coins);
            Assert.IsNull(SaveMigrator.FromJson("not json"));
            Assert.IsNull(SaveMigrator.FromJson(""));
            Assert.IsNull(SaveMigrator.FromJson("{\"version\":99}"), "saves from a newer build are refused");
        }
    }
}
