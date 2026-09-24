using System.Collections.Generic;
using NUnit.Framework;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class DailyCosmeticsSaveTests
    {
        private static SaveData Blob(long updated, params string[] owned)
        {
            var s = new SaveData { updatedAtUnixMs = updated };
            s.progress.cosmetics = new List<string>(owned);
            return s;
        }

        [Test]
        public void OwnedCosmeticsAreAUnionAndSelectionFollowsTheNewerSave()
        {
            var a = Blob(100, "paint:snow"); a.progress.paint = "snow";
            var b = Blob(200, "livery:hood", "paint:snow"); b.progress.paint = "rosso"; b.progress.livery = "hood";
            var m = SaveMerge.Merge(a, b);
            CollectionAssert.AreEquivalent(new[] { "paint:snow", "livery:hood" }, m.progress.cosmetics);
            Assert.AreEqual("rosso", m.progress.paint);
            Assert.AreEqual("hood", m.progress.livery);
        }

        [Test]
        public void TwoDevicesThatBothPaidTodaysBonusKeepOnePayment()
        {
            var phone = Blob(500); phone.progress.daily.finishDays.Add(266); phone.progress.daily.claims.Add(new DailyClaim { day = 266, coins = 150 });
            var desktop = Blob(900); desktop.progress.daily.finishDays.Add(266); desktop.progress.daily.claims.Add(new DailyClaim { day = 266, coins = 150 });
            Assert.AreEqual(150, phone.CoinsEarned);
            var m = SaveMerge.Merge(phone, desktop);
            Assert.AreEqual(1, m.progress.daily.claims.Count);
            Assert.AreEqual(150, m.CoinsEarned, "one bonus, not two");
            Assert.IsTrue(m.progress.daily.Claimed(266));
        }

        [Test]
        public void StreakSurvivesAMergeOfDevicesThatSawDifferentDays()
        {
            var a = Blob(100); a.progress.daily.finishDays.AddRange(new[] { 262, 263, 264, 265 });
            var b = Blob(900); b.progress.daily.finishDays.AddRange(new[] { 264, 266 });
            var m = SaveMerge.Merge(a, b);
            Assert.AreEqual(5, Core.Daily.DailyChallenge.StreakEndingAt(m.progress.daily.finishDays, 266));
        }

        [Test]
        public void MergedFinishDaysKeepAMonth()
        {
            var a = Blob(100); for (int d = 200; d < 260; d++) a.progress.daily.finishDays.Add(d);
            var m = SaveMerge.Merge(a, Blob(50));
            Assert.AreEqual(DailyData.KeepDays, m.progress.daily.finishDays.Count);
            Assert.AreEqual(259, m.progress.daily.finishDays[m.progress.daily.finishDays.Count - 1]);
        }

        [Test]
        public void MissingDailyDataMergesToDefaults()
        {
            var a = new SaveData(); a.progress.daily = null; a.progress.cosmetics = null;
            var b = new SaveData();
            var m = SaveMerge.Merge(a, b);
            Assert.IsNotNull(m.progress.daily);
            Assert.IsNotNull(m.progress.cosmetics);
        }
    }
}
