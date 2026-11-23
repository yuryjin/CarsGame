using System;
using System.Text;
using NUnit.Framework;
using RacingGame.Core.Ghost;
using RacingGame.Core.Save;
using UnityEngine;

namespace RacingGame.Tests
{
    public sealed class PersonalGhostsTests
    {
        private static string Replay(int hash, double seconds, string name)
        {
            var record = new GhostRecord { TrackHash = hash, PlayerName = name };
            record.AddPose(Vector3.zero, Quaternion.identity, 0, 0, 0);
            record.AddPose(Vector3.right, Quaternion.identity, seconds, 0, 0);
            return record.ToBase64();
        }

        private static SaveData Legacy(string replay) =>
            SaveMigrator.FromJson("{\"version\":6,\"bestGhost\":\"" + replay + "\"}");

        [Test]
        public void SectorTrainingSurvivesRestartAndMergesMinimaOnlyWithinItsRoute()
        {
            var a = new SaveData();
            var first = new EventBest { eventId = "e1" };
            a.progress.bests.Add(first);
            var lap = first.KeepSectors(10, 2, new[] { 4000, 2400, 2800 });
            Assert.AreEqual(9200, lap.LapMilliseconds);
            var other = first.KeepSectors(11, 2, new[] { 700, 0, 0 });
            Assert.AreEqual(0, other.LapMilliseconds);
            first.KeepSectors(10, 1, new[] { 1, 1, 1 });
            var b = new SaveData();
            var second = new EventBest { eventId = "e1" };
            b.progress.bests.Add(second);
            second.KeepSectors(10, 2, new[] { 0, 2200, 4000 });
            var merged = SaveMigrator.FromJson(SaveMigrator.ToJson(SaveMerge.Merge(a,b))).progress.bests[0];
            CollectionAssert.AreEqual(new[] { 4000, 2200, 2800 }, merged.sectors.Find(s => s.trackHash == 10 && s.laps == 2).milliseconds);
            Assert.AreEqual(0, merged.sectors.Find(s => s.trackHash == 11).LapMilliseconds);
            Assert.AreEqual(3, merged.sectors.Find(s => s.laps == 1).LapMilliseconds);
            Assert.AreEqual(9200, lap.LapMilliseconds, "merging another device cannot mutate its input");
        }

        [Test]
        public void RouteIncludesEventLayoutAndLapsAndEachDeviceCanContributeAWinner()
        {
            var local = new SaveData { updatedAtUnixMs = 200 };
            var cloud = new SaveData { updatedAtUnixMs = 100 };
            local.personalGhosts.KeepBest("e1", 2, Replay(11, 6, "local e1"), 1);
            local.personalGhosts.KeepBest("e2", 2, Replay(22, 12, "local e2"), 2);
            cloud.personalGhosts.KeepBest("e1", 2, Replay(11, 8, "cloud e1"), 5);
            cloud.personalGhosts.KeepBest("e2", 2, Replay(22, 9, "cloud e2"), 6);
            cloud.personalGhosts.KeepBest("e1", 1, Replay(11, 3, "one lap"), 3);
            cloud.personalGhosts.KeepBest("e1", 2, Replay(99, 2, "previous layout"), 4);
            Assert.IsFalse(local.personalGhosts.KeepBest("e1", 2, Replay(11, 15, "slower retry"), 10));
            foreach (var save in new[] { SaveMerge.Merge(local, cloud), SaveMerge.Merge(cloud, local) })
            {
                var ghosts = SaveMigrator.FromJson(SaveMigrator.ToJson(save)).personalGhosts;
                Assert.AreEqual("local e1", ghosts.Find("e1", 11, 2).PlayerName);
                Assert.AreEqual("cloud e2", ghosts.Find("e2", 22, 2).PlayerName);
                Assert.AreEqual("one lap", ghosts.Find("e1", 11, 1).PlayerName);
                Assert.AreEqual("previous layout", ghosts.Find("e1", 99, 2).PlayerName);
                Assert.IsNull(ghosts.Find("e2", 11, 2));
                Assert.IsNull(ghosts.Find("e1", 11, 3));
            }
        }

        [Test]
        public void BothOldCodecsSurviveCloudMergeUntilTheCircuitCanVerifyEachReplay()
        {
            const string v1 = "H4sIAAAAAAAC/2OsZmBgMGJgZfNJTU9MrmQC8sT//P8vAqRPA7FNTyNjNZSvz8jAkMJQz1D6S7YSAL7VRwY3AAAA";
            var old = SaveMigrator.FromJson("{\"bestGhost\":\"" + v1 + "\"}");
            string v2 = Replay(123, 8, "v2");
            var merged = SaveMerge.Merge(old, Legacy(v2));
            var ghosts = SaveMigrator.FromJson(SaveMigrator.ToJson(merged)).personalGhosts;
            Assert.IsFalse(ghosts.ResolveLegacy("e2", 321, 1, _ => AssertNeverCalled()));
            Assert.IsFalse(ghosts.ResolveLegacy("e1", 123, 3, _ => false));
            Assert.IsNull(ghosts.Find("e1", 123, 3));
            Assert.IsTrue(ghosts.ResolveLegacy("e1", 123, 1, record => record.PlayerName == "Legacy"));
            Assert.AreEqual(200, ghosts.Find("e1", 123, 1).DurationMilliseconds);
            Assert.IsTrue(ghosts.ResolveLegacy("e1", 123, 2, record => record.PlayerName == "v2"));
            Assert.AreEqual(8000, ghosts.Find("e1", 123, 2).DurationMilliseconds);
            var resolved = new SaveData { personalGhosts = ghosts };
            Assert.AreEqual(JsonUtility.ToJson(ghosts), JsonUtility.ToJson(SaveMerge.Merge(resolved, old).personalGhosts));
        }

        private static bool AssertNeverCalled() { Assert.Fail("Wrong layout was sent for verification"); return false; }

        [Test]
        public void BadReplayCannotBeatValidEntriesAndSerializedIndexIsRebuiltFromThePayload()
        {
            var save = new SaveData();
            string good = Replay(55, 5, "intact");
            save.personalGhosts.KeepBest("e1", 2, good, 1);
            Assert.IsFalse(save.personalGhosts.KeepBest("e1", 2, "bad", 2));
            Assert.IsFalse(save.personalGhosts.KeepBest("e1", 2, Convert.ToBase64String(new byte[64]), 2));
            save.Local("A").coinsEarned = 120;
            string corruptedIndex = SaveMigrator.ToJson(save).Replace("\"durationMs\":5000", "\"durationMs\":1")
                .Replace("\"trackHash\":55", "\"trackHash\":0");
            var restored = SaveMigrator.FromJson(corruptedIndex);
            Assert.AreEqual(5000, restored.personalGhosts.Find("e1", 55, 2).DurationMilliseconds);
            Assert.IsNull(restored.personalGhosts.Find("e1", 0, 2));
            restored = SaveMigrator.FromJson(corruptedIndex.Replace(good, "bad"));
            Assert.IsNull(restored.personalGhosts.Find("e1", 55, 2));
            Assert.AreEqual(120, restored.CoinsEarned);
        }

        [Test]
        public void FullArchivesPreserveTheSameBoundedSelectionAcrossMergeOrderRestartAndRetry()
        {
            var a = new SaveData();
            var b = new SaveData();
            int combined = 0;
            for (int k = 1; k <= 6; k++)
            {
                var random = new System.Random(k);
                var record = new GhostRecord { TrackHash = k };
                for (int i = 0; i < 3200; i++)
                    record.AddPose(new Vector3(random.Next(50000), random.Next(50000), random.Next(50000)),
                        Quaternion.identity, i / 10d, 0f, 0);
                string payload = record.ToBase64();
                Assert.IsNotEmpty(payload);
                combined += payload.Length;
                (k <= 3 ? a : b).personalGhosts.KeepBest("e" + k, 2, payload, k);
            }
            Assert.Greater(combined, PersonalGhosts.MaxSerializedBytes);
            var merged = SaveMerge.Merge(a, b);
            string json = JsonUtility.ToJson(merged.personalGhosts);
            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(json), PersonalGhosts.MaxSerializedBytes);
            Assert.NotNull(merged.personalGhosts.Find("e6", 6, 2));
            Assert.IsNull(merged.personalGhosts.Find("e1", 1, 2));
            Assert.AreEqual(json, JsonUtility.ToJson(SaveMerge.Merge(b, a).personalGhosts));
            var restart = SaveMigrator.FromJson(SaveMigrator.ToJson(merged));
            Assert.AreEqual(json, JsonUtility.ToJson(SaveMerge.Merge(SaveMerge.Merge(restart, b), a).personalGhosts));
        }
    }
}
