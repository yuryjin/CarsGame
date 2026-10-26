using System.Linq;
using NUnit.Framework;
using RacingGame.Core.Save;
using UnityEngine;

namespace RacingGame.Tests
{
    public class SaveMergeInvariantTests
    {
        // Fixed seeds keep failures reproducible. Shared device rows model stale cloud snapshots.
        private static SaveData Snapshot(int seed, long timestamp)
        {
            var random = new System.Random(seed);
            var save = new SaveData { deviceId = "device-" + seed, updatedAtUnixMs = timestamp };
            save.settings.lang = seed % 2 == 0 ? "ru" : "en";
            save.settings.touchLeftHanded = seed % 2 == 0;
            save.progress.paint = "paint-" + seed;
            for (int i = 0; i < 3; i++)
            {
                var row = save.Local("device-" + i);
                row.coinsEarned = random.Next(1000, 10000);
                row.coinsSpent = random.Next(0, 1000);
                row.races = random.Next(10, 100);
                row.wins = random.Next(0, 10);
                row.kills = random.Next(0, 100);
                row.deaths = random.Next(0, 100);
                row.distanceKm = random.Next(0, 1000) / 4d;
                save.progress.upgrades[i] = random.Next(0, 4);
                save.progress.weapons[i] = random.Next(0, 4);
            }
            for (int i = 1; i <= 6; i++)
            {
                string id = "e" + i;
                if (random.Next(2) == 0) save.progress.unlockedEvents.Add(id);
                save.progress.bests.Add(new EventBest { eventId = id,
                    bestTimeMs = random.Next(0, 4) * 10000L, stars = random.Next(0, 8),
                    setup = new RecordSetup { captured = true, tier = "easy", body = "classic",
                        upgrades = new[] { seed % 4, 1, 2 }, weapons = new[] { 1, 0, 0, 0 } } });
                if (random.Next(2) == 0) save.progress.cosmetics.Add("paint:" + i);
            }
            for (int day = 20000; day < 20045; day++)
            {
                if (random.Next(2) == 0) save.progress.daily.finishDays.Add(day);
                if (random.Next(2) == 0) save.progress.daily.claims.Add(new DailyClaim { day = day, coins = random.Next(1, 4) * 50 });
            }
            return save;
        }

        private static string Json(object value) => JsonUtility.ToJson(value);

        [TestCase(7)]
        [TestCase(42)]
        [TestCase(2026)]
        public void ProgressConvergesForEveryDeliveryOrderAndGrouping(int seed)
        {
            var saves = new[] { Snapshot(seed, 100), Snapshot(seed + 1, 200), Snapshot(seed + 2, 300) };
            string expected = Json(SaveMerge.Merge(SaveMerge.Merge(saves[0], saves[1]), saves[2]));
            int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
            foreach (var order in orders)
            {
                var a = saves[order[0]]; var b = saves[order[1]]; var c = saves[order[2]];
                Assert.AreEqual(expected, Json(SaveMerge.Merge(SaveMerge.Merge(a, b), c)), "left grouping " + string.Join(",", order));
                Assert.AreEqual(expected, Json(SaveMerge.Merge(a, SaveMerge.Merge(b, c))), "right grouping " + string.Join(",", order));
            }
        }

        [TestCase(7)]
        [TestCase(42)]
        [TestCase(2026)]
        public void RepeatedSyncAndStaleSnapshotsDoNotDuplicateRewardsOrLoseProgress(int seed)
        {
            var local = Snapshot(seed, 100);
            var cloud = Snapshot(seed + 1, 200);
            var merged = SaveMerge.Merge(local, cloud);
            string expected = Json(merged);
            for (int i = 0; i < 10; i++)
            {
                merged = SaveMerge.Merge(SaveMerge.Merge(merged, local), cloud);
                merged = SaveMerge.Merge(merged, merged);
                // Exercise the actual persistence boundary between synchronizations.
                merged = SaveMigrator.FromJson(SaveMigrator.ToJson(merged));
                Assert.AreEqual(expected, Json(merged), "sync " + i);
            }
        }

        [Test]
        public void MergeAndSubsequentEditsDoNotMutateEitherInputSnapshot()
        {
            var local = Snapshot(7, 100); var cloud = Snapshot(42, 200);
            string localBefore = Json(local), cloudBefore = Json(cloud);
            var merged = SaveMerge.Merge(local, cloud);
            merged.settings.lang = "changed";
            merged.progress.ledger[0].coinsSpent++;
            merged.progress.upgrades[0]++;
            merged.progress.weapons[0]++;
            merged.progress.bests.First(b => b.setup != null).setup.upgrades[0]++;
            merged.progress.bests.First(b => b.setup != null).setup.weapons[0]++;
            merged.progress.bests[0].stars = 0;
            merged.progress.cosmetics.Clear();
            merged.progress.unlockedEvents.Clear();
            merged.progress.daily.finishDays.Clear();
            merged.progress.daily.claims[0].coins++;
            Assert.AreEqual(localBefore, Json(local));
            Assert.AreEqual(cloudBefore, Json(cloud));
        }

        [Test]
        public void EqualTimestampSnapshotsStillConvergeOnMonotonicProgress()
        {
            // Settings intentionally prefer the first input on a timestamp tie; progress must still converge.
            var a = Snapshot(7, 100); var b = Snapshot(42, 100);
            var ab = SaveMerge.Merge(a, b); var ba = SaveMerge.Merge(b, a);
            Assert.AreEqual(ab.progress.ledger.Count, ba.progress.ledger.Count);
            for (int i = 0; i < ab.progress.ledger.Count; i++)
                Assert.AreEqual(Json(ab.progress.ledger[i]), Json(ba.progress.ledger[i]));
            CollectionAssert.AreEqual(ab.progress.unlockedEvents, ba.progress.unlockedEvents);
            CollectionAssert.AreEqual(ab.progress.cosmetics, ba.progress.cosmetics);
            CollectionAssert.AreEqual(ab.progress.upgrades, ba.progress.upgrades);
            CollectionAssert.AreEqual(ab.progress.weapons, ba.progress.weapons);
            Assert.AreEqual(Json(ab.progress.daily), Json(ba.progress.daily));
            Assert.AreEqual(ab.Coins, ba.Coins);
            for (int i = 0; i < ab.progress.bests.Count; i++)
                Assert.AreEqual(Json(ab.progress.bests[i]), Json(ba.progress.bests[i]));
        }
    }
}
