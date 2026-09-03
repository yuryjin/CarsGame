using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Ghost;

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
    }
}
