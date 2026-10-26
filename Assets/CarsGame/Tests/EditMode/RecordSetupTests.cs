using NUnit.Framework;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class RecordSetupTests
    {
        private static SaveData Record(long ms, string tier, int engine = 0)
        {
            var data = new SaveData();
            data.progress.bests.Add(new EventBest { eventId = "e1", bestTimeMs = ms, stars = 1,
                setup = tier == null ? null : new RecordSetup { captured = true, tier = tier, body = "classic", laps = 3,
                    upgrades = new[] { engine, 0, 0 }, weapons = new int[4] } });
            return data;
        }

        [Test]
        public void FasterTimeKeepsItsOwnConditionsAndDoesNotAliasEitherSave()
        {
            var fast = Record(60000, "hard", 2);
            var slow = Record(65000, "easy", 1);
            slow.progress.bests[0].stars = 6;
            var merged = SaveMerge.Merge(slow, fast).progress.bests[0];
            Assert.AreEqual(60000, merged.bestTimeMs);
            Assert.AreEqual("hard", merged.setup.tier);
            Assert.AreEqual(2, merged.setup.upgrades[0]);
            Assert.AreEqual(7, merged.stars);
            merged.setup.upgrades[0] = 3;
            Assert.AreEqual(2, fast.progress.bests[0].setup.upgrades[0]);
            Assert.AreEqual(1, slow.progress.bests[0].setup.upgrades[0]);
        }

        [Test]
        public void EqualTimeProvenanceMergeIsCommutativeAndPrefersKnownConditions()
        {
            var a = Record(60000, "hard");
            var b = Record(60000, "easy");
            var ab = SaveMerge.Merge(a, b).progress.bests[0];
            var ba = SaveMerge.Merge(b, a).progress.bests[0];
            Assert.IsTrue(RecordSetup.Same(ab.setup, ba.setup));
            var old = Record(60000, null);
            Assert.IsTrue(RecordSetup.Same(a.progress.bests[0].setup, SaveMerge.Merge(old, a).progress.bests[0].setup));
            Assert.IsTrue(RecordSetup.Same(a.progress.bests[0].setup, SaveMerge.Merge(a, old).progress.bests[0].setup));
        }

        [Test]
        public void FasterLegacyRecordNeverBorrowsSlowerRecordsConditions()
        {
            var old = Record(55000, null);
            var current = Record(60000, "easy");
            Assert.IsNull(SaveMerge.Merge(old, current).progress.bests[0].setup);
            Assert.IsNull(SaveMerge.Merge(current, old).progress.bests[0].setup);
            var migrated = SaveMigrator.FromJson("{\"version\":5,\"progress\":{\"bests\":[{\"eventId\":\"e1\",\"bestTimeMs\":55000,\"stars\":3}]}}");
            Assert.AreEqual(55000, migrated.progress.bests[0].bestTimeMs);
            Assert.AreEqual(3, migrated.progress.bests[0].stars);
            Assert.IsNull(migrated.progress.bests[0].setup);
            var restoredAgain = SaveMigrator.FromJson(SaveMigrator.ToJson(migrated));
            Assert.IsNull(restoredAgain.progress.bests[0].setup, "Resaving a legacy record must not invent conditions");
        }

        [Test]
        public void ComparisonDetectsDrivingAndWeaponChangesAndSurvivesRoundTrip()
        {
            var data = Record(60000, "normal", 2);
            var original = data.progress.bests[0].setup;
            var restored = SaveMigrator.FromJson(SaveMigrator.ToJson(data)).progress.bests[0].setup;
            Assert.IsTrue(RecordSetup.Same(original, restored));
            Assert.IsFalse(RecordSetup.Same(null, null), "Two unknown setups cannot establish comparable conditions");
            var changed = original.Copy(); changed.body = "bulwark"; Assert.IsFalse(RecordSetup.Same(original, changed));
            changed = original.Copy(); changed.upgrades[0]++; Assert.IsFalse(RecordSetup.Same(original, changed));
            changed = original.Copy(); changed.weapons[2]++; Assert.IsFalse(RecordSetup.Same(original, changed));
            changed = original.Copy(); changed.primary++; Assert.IsFalse(RecordSetup.Same(original, changed));
            changed = original.Copy(); changed.continued = true; Assert.IsFalse(RecordSetup.Same(original, changed));
            changed = original.Copy(); changed.trackHash++; Assert.IsFalse(RecordSetup.Same(original, changed));
            changed = original.Copy(); changed.laps++; Assert.IsFalse(RecordSetup.Same(original, changed));
        }
    }
}
