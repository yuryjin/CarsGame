using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Ghost;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RacingGame.Tests
{
    public class GhostRecordTests
    {
        private static GhostRecord Make(int n)
        {
            var g = new GhostRecord { TrackHash = 12345, PlayerName = "Тест" };
            for (int i = 0; i < n; i++) g.Add(new Vector3(i * 0.7f, 0.05f, -20f + i * 0.3f), (i * 3f) % 360f, Mathf.Sin(i * 0.1f), (byte)(i % 2));
            return g;
        }

        [Test]
        public void RoundTripKeepsPosesWithinQuantisation()
        {
            var g = Make(600);   // 60 s at 10 Hz
            var back = GhostRecord.FromBytes(g.ToBytes());
            Assert.IsNotNull(back);
            Assert.AreEqual(600, back.Samples.Count);
            Assert.AreEqual("Тест", back.PlayerName);
            Assert.AreEqual(12345, back.TrackHash);
            for (int i = 0; i < 600; i += 37)
            {
                Assert.Less(Vector3.Distance(g.Samples[i].Position, back.Samples[i].Position), 0.02f);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(g.Samples[i].YawDegrees, back.Samples[i].YawDegrees)), 0.02f);
            }
        }

        [Test]
        public void SixtySecondsStaysSmall()
        {
            var bytes = Make(600).ToBytes();
            Assert.Less(bytes.Length, 12000, "≈10 KB raw budget; base64 must stay well under the 200 KB session limit");
            Assert.Less(Make(600).ToBase64().Length, 16000);
        }

        [Test]
        public void CorruptOrWrongVersionIsRejected()
        {
            var bytes = Make(50).ToBytes();
            bytes[bytes.Length / 2] ^= 0xFF;
            Assert.IsNull(GhostRecord.FromBytes(bytes));
            Assert.IsNull(GhostRecord.FromBase64("not base64!!"));
            Assert.IsNull(GhostRecord.FromBytes(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void EvaluateInterpolatesAndClamps()
        {
            var g = new GhostRecord { SampleStep = 5, TickRate = 50 };
            g.Add(Vector3.zero, 0f, 0f, 0); g.Add(new Vector3(10f, 0f, 0f), 90f, 0f, 0);
            Assert.IsTrue(g.Evaluate(0.05f, out var p, out var yaw));   // halfway through the 0.1 s step
            Assert.AreEqual(5f, p.x, 0.01f); Assert.AreEqual(45f, yaw, 0.01f);
            g.Evaluate(99f, out p, out _); Assert.AreEqual(10f, p.x, 0.01f);
        }

        [Test]
        public void FixedVersionOneFixtureRemainsReadableAndCanBeResaved()
        {
            const string legacy = "H4sIAAAAAAAC/2OsZmBgMGJgZfNJTU9MrmQC8sT//P8vAqRPA7FNTyNjNZSvz8jAkMJQz1D6S7YSAL7VRwY3AAAA";
            var record = GhostRecord.FromBase64(legacy);
            Assert.NotNull(record);
            Assert.AreEqual(123, record.TrackHash);
            Assert.AreEqual("Legacy", record.PlayerName);
            Assert.AreEqual(2, record.Samples.Count);
            Assert.AreEqual(.2f, record.Duration, .0001f, "Keep the old count-based duration");
            Assert.AreEqual(-10.01f, record.Samples[0].Position.x, .0001f);
            record.EvaluatePose(.05f, out var position, out var rotation);
            Assert.AreEqual(-9.51f, position.x, .001f);
            Assert.Less(Quaternion.Angle(Quaternion.identity, rotation), .05f, "359°→1° uses the short arc");
            var restored = GhostRecord.FromBytes(record.ToBytes());
            Assert.NotNull(restored);
            Assert.AreEqual(record.Duration, restored.Duration);
            Assert.AreEqual(record.Samples.Count, restored.Samples.Count);
        }

        [Test]
        public void QuaternionPosesAndUnicodeNamesSurviveTheCompactFormat()
        {
            var record = new GhostRecord { PlayerName = new string('车', 100) + "🚗", BodyId = "tortoise" };
            for (int i = 0; i < 120; i++)
                record.AddPose(new Vector3(i * .13f, Mathf.Sin(i * .1f) * 11f, -i),
                    Quaternion.Euler(Mathf.Sin(i * .08f) * 70f, i * 7f, i * 4f), i * .137, .35f, 0);
            var restored = GhostRecord.FromBytes(record.ToBytes());
            Assert.NotNull(restored);
            Assert.AreEqual("tortoise", restored.BodyId);
            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(restored.PlayerName), 255);
            Assert.AreEqual(new string('车', 85), restored.PlayerName, "The byte limit must retain whole UTF-8 characters");
            Assert.AreEqual(-1, restored.PlayerName.IndexOf('\uFFFD'), "No replacement character may be introduced");
            for (int i = 0; i < record.Samples.Count; i++)
            {
                Assert.AreEqual(record.Samples[i].TimeMilliseconds, restored.Samples[i].TimeMilliseconds);
                Assert.Less(Quaternion.Angle(record.Samples[i].Rotation, restored.Samples[i].Rotation), .1f);
                Assert.Less(Vector3.Distance(record.Samples[i].Position, restored.Samples[i].Position), .01f);
            }
            Assert.AreEqual(record.Duration, restored.Duration);
        }

        [Test]
        public void ReplayUsesIrregularRaceTimesInsteadOfCountingFixedTicks()
        {
            var record = new GhostRecord();
            record.AddPose(Vector3.zero, Quaternion.identity, 0d, 0f, 0);
            record.AddPose(Vector3.right * 10f, Quaternion.Euler(0, 90, 0), .1d, 0f, 0);
            record.AddPose(Vector3.right * 20f, Quaternion.Euler(0, 180, 0), .4d, 0f, 0);
            record = GhostRecord.FromBytes(record.ToBytes());
            Assert.NotNull(record);
            record.EvaluatePose(.25f, out var position, out var rotation);
            Assert.AreEqual(15f, position.x, .001f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 135, 0), rotation), .05f);
            Assert.AreEqual(.4f, record.Duration, .0001f);
        }

        [Test]
        public void RecoveryCutsDoNotInterpolateAcrossTheWorld()
        {
            var record = new GhostRecord();
            record.AddPose(Vector3.zero, Quaternion.identity, 0d, 0f, 0);
            record.AddPose(Vector3.right * 10f, Quaternion.identity, .1d, 0f, 0);
            record.AddPose(Vector3.right * 1000f, Quaternion.Euler(0, 90, 0), .2d, 0f, GhostRecord.TeleportFlag);
            record.AddPose(Vector3.right * 1010f, Quaternion.Euler(0, 90, 0), .3d, 0f, 0);
            record = GhostRecord.FromBytes(record.ToBytes());
            Assert.NotNull(record);
            record.EvaluatePose(.15f, out var position, out _);
            Assert.AreEqual(10f, position.x);
            record.EvaluatePose(.2f, out position, out _);
            Assert.AreEqual(1000f, position.x);
            record.EvaluatePose(.25f, out position, out _);
            Assert.AreEqual(1005f, position.x, .001f);
        }

        [Test]
        public void SameMillisecondFinalPoseReplacesTheSampleAndPreservesItsCut()
        {
            var record = new GhostRecord();
            record.AddPose(Vector3.zero, Quaternion.identity, 0d, 0f, 0);
            record.AddPose(Vector3.right, Quaternion.identity, .1d, 0f, GhostRecord.TeleportFlag);
            record.AddPose(Vector3.right * 2f, Quaternion.identity, .1001d, 0f, 0);
            Assert.AreEqual(2, record.Samples.Count);
            Assert.AreEqual(GhostRecord.TeleportFlag, record.Samples[1].Flags);
            Assert.AreEqual(2f, record.Samples[1].Position.x);
            Assert.Throws<ArgumentOutOfRangeException>(() => record.AddPose(Vector3.zero, Quaternion.identity, .05d, 0f, 0));
            Assert.NotNull(GhostRecord.FromBytes(record.ToBytes()));
        }

        private static byte[] Raw(byte[] bytes)
        {
            using (var input = new MemoryStream(bytes)) using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream()) { gzip.CopyTo(output); return output.ToArray(); }
        }
        private static byte[] Pack(byte[] bytes)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true)) gzip.Write(bytes, 0, bytes.Length);
                return output.ToArray();
            }
        }
        private static void RepairChecksum(byte[] bytes)
        {
            uint hash = 2166136261u;
            unchecked { for (int i = 0; i < bytes.Length - 4; i++) hash = (hash ^ bytes[i]) * 16777619u; }
            BitConverter.GetBytes(hash).CopyTo(bytes, bytes.Length - 4);
        }

        [Test]
        public void ChecksumCoversTheHeaderAndHeightAndDecodeRejectsInvalidTimelines()
        {
            byte[] original = Raw(Make(3).ToBytes());
            int firstSample = 18 + original[8] + original[9 + original[8]];
            foreach (int offset in new[] { 1, firstSample + 8 })
            {
                var corrupt = (byte[])original.Clone(); corrupt[offset] ^= 1;
                Assert.IsNull(GhostRecord.FromBytes(Pack(corrupt)), "Header and height must participate in the checksum");
            }
            var backwards = (byte[])original.Clone();
            BitConverter.GetBytes(-1).CopyTo(backwards, firstSample + 26);
            RepairChecksum(backwards);
            Assert.IsNull(GhostRecord.FromBytes(Pack(backwards)), "Valid checksum cannot legitimise backwards timestamps");
            var zeroRotation = (byte[])original.Clone();
            Array.Clear(zeroRotation, firstSample + 16, 8); RepairChecksum(zeroRotation);
            Assert.IsNull(GhostRecord.FromBytes(Pack(zeroRotation)), "A zero quaternion is not a pose");
        }

        [Test]
        public void BoundedCodecRejectsExcessiveExpansionAndNonFinitePoses()
        {
            Assert.IsNull(GhostRecord.FromBytes(Pack(new byte[1024 * 1024])));
            var record = Make(2);
            var invalid = record.Samples[1]; invalid.Position.x = float.NaN; record.Samples[1] = invalid;
            Assert.IsEmpty(record.ToBytes());
            Assert.AreEqual("", record.ToBase64());
        }

        [Test]
        public void OversizedCompressedRecordIsNotPublishedAsAnUnreadablePayload()
        {
            var random = new System.Random(12345);
            var record = new GhostRecord();
            for (int i = 0; i < GhostRecord.MaxSamples; i++)
                record.AddPose(new Vector3(random.Next(-100000, 100000) / 100f, random.Next(-100000, 100000) / 100f,
                    random.Next(-100000, 100000) / 100f), Quaternion.Euler(random.Next(360), random.Next(360), random.Next(360)),
                    i * .1d, (float)random.NextDouble(), (byte)random.Next(4));
            Assert.IsEmpty(record.ToBytes());
            Assert.AreEqual("", record.ToBase64());
        }
    }
}
