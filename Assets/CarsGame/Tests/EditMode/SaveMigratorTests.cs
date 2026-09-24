using NUnit.Framework;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class SaveMigratorTests
    {
        [Test]
        public void ParseEmptyOrWhitespaceReturnsNullAndEmptyStatus()
        {
            var data1 = SaveMigrator.Parse(null, out var status1);
            Assert.IsNull(data1);
            Assert.AreEqual(SaveParseStatus.Empty, status1);

            var data2 = SaveMigrator.Parse("", out var status2);
            Assert.IsNull(data2);
            Assert.AreEqual(SaveParseStatus.Empty, status2);

            var data3 = SaveMigrator.Parse("   \t\n  ", out var status3);
            Assert.IsNull(data3);
            Assert.AreEqual(SaveParseStatus.Empty, status3);
        }

        [Test]
        public void ParseCorruptJsonReturnsNullAndCorruptStatus()
        {
            var data = SaveMigrator.Parse("{ not valid json :;; }", out var status);
            Assert.IsNull(data);
            Assert.AreEqual(SaveParseStatus.Corrupt, status);
        }

        [Test]
        public void ParseNewerVersionReturnsNullAndNewerVersionStatus()
        {
            string futureJson = "{\"version\":999,\"deviceId\":\"future_device\"}";
            var data = SaveMigrator.Parse(futureJson, out var status);
            Assert.IsNull(data);
            Assert.AreEqual(SaveParseStatus.NewerVersion, status);
        }

        [Test]
        public void ParseValidMinimalJsonInitializesCollectionsAndCurrentVersion()
        {
            string minimalJson = "{\"deviceId\":\"d1\"}";
            var data = SaveMigrator.Parse(minimalJson, out var status);

            Assert.AreEqual(SaveParseStatus.Ok, status);
            Assert.NotNull(data);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual("d1", data.deviceId);
            Assert.NotNull(data.settings);
            Assert.NotNull(data.progress);
            Assert.NotNull(data.progress.upgrades);
            Assert.AreEqual(3, data.progress.upgrades.Length);
            Assert.NotNull(data.progress.weapons);
            Assert.AreEqual(4, data.progress.weapons.Length, "WeaponKind has four garage lines (remote charge)");
            Assert.NotNull(data.progress.unlockedEvents);
            Assert.NotNull(data.progress.bests);
            Assert.NotNull(data.progress.ledger);
        }

        [Test]
        public void ParseV1AndV2MigratesToV3WithWeapons()
        {
            string v1Json = "{\"version\":1,\"deviceId\":\"d1\",\"progress\":{\"upgrades\":[1,2,0]}}";
            var data1 = SaveMigrator.Parse(v1Json, out var status1);
            Assert.AreEqual(SaveParseStatus.Ok, status1);
            Assert.AreEqual(SaveData.CurrentVersion, data1.version);
            Assert.AreEqual(4, data1.progress.weapons.Length);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, data1.progress.upgrades);

            string v2Json = "{\"version\":2,\"deviceId\":\"d2\",\"progress\":{\"upgrades\":[2,1,1]}}";
            var data2 = SaveMigrator.Parse(v2Json, out var status2);
            Assert.AreEqual(SaveParseStatus.Ok, status2);
            Assert.AreEqual(SaveData.CurrentVersion, data2.version);
            Assert.AreEqual(4, data2.progress.weapons.Length);
            CollectionAssert.AreEqual(new[] { 2, 1, 1 }, data2.progress.upgrades);
        }

        [Test]
        public void ToJsonAndFromJsonRoundTrip()
        {
            var original = new SaveData { deviceId = "roundtrip_device" };
            original.Local("roundtrip_device").coinsEarned = 777;
            original.progress.unlockedEvents.Add("event_desert_1");
            original.progress.weapons = new[] { 2, 1, 0 };
            original.progress.equippedSecondary = 1;
            original.progress.equippedPrimary = 1;

            string json = SaveMigrator.ToJson(original);
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var restored = SaveMigrator.FromJson(json);
            Assert.NotNull(restored);
            Assert.AreEqual("roundtrip_device", restored.deviceId);
            Assert.AreEqual(777, restored.CoinsEarned);
            Assert.Contains("event_desert_1", restored.progress.unlockedEvents);
            CollectionAssert.AreEqual(new[] { 2, 1, 0, 0 }, restored.progress.weapons, "a 3-slot save gains the charge slot");
            Assert.AreEqual(1, restored.progress.equippedSecondary);
            Assert.AreEqual(1, restored.progress.equippedPrimary);
        }
    }
}
