using System;
using NUnit.Framework;
using RacingGame.Core.Daily;

namespace RacingGame.Tests
{
    public class DailyChallengeTests
    {
        [Test]
        public void SpecIsDeterministicPerDay()
        {
            var a = DailyChallenge.For(271); var b = DailyChallenge.For(271);
            Assert.AreEqual(a.Twist, b.Twist); Assert.AreEqual(a.Reversed, b.Reversed); Assert.AreEqual(a.Sky, b.Sky);
            Assert.AreEqual(a.Seed, b.Seed); Assert.AreEqual("d271", a.EventId);
        }

        [Test]
        public void EveryTwistOnceAWeekAndNeverTwiceInARow()
        {
            for (int week = -3; week < 60; week++)
            {
                var seen = new System.Collections.Generic.HashSet<RaceTwist>();
                for (int s = 0; s < 6; s++) seen.Add(DailyChallenge.For(week * 6 + s).Twist);
                Assert.AreEqual(6, seen.Count, "week " + week);
            }
            for (int d = -20; d < 400; d++) Assert.AreNotEqual(DailyChallenge.For(d).Twist, DailyChallenge.For(d + 1).Twist, "day " + d);
        }

        [Test]
        public void TwistRulesShapeTheRace()
        {
            for (int d = 0; d < 60; d++)
            {
                var s = DailyChallenge.For(d);
                Assert.AreEqual(s.Twist == RaceTwist.Night, s.Sky == DailyChallenge.SkyNight, "night sky only on night days");
                Assert.AreEqual(s.Twist == RaceTwist.Sprint ? 2 : 3, s.Laps);
                Assert.AreEqual(s.Twist == RaceTwist.DoubleDamage ? 2f : 1f, s.DamageMultiplier);
                Assert.AreEqual(s.Twist == RaceTwist.Arsenal ? 18 : 9, s.PickupCount);
                Assert.GreaterOrEqual(s.Seed, 0);
            }
        }

        [Test]
        public void DayIndexUsesUtcDays()
        {
            Assert.AreEqual(0, DailyChallenge.DayIndex(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual(0, DailyChallenge.DayIndex(new DateTime(2026, 1, 1, 23, 59, 59, DateTimeKind.Utc)));
            Assert.AreEqual(266, DailyChallenge.DayIndex(new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc)));
            long ms = DailyChallenge.DayStartUnixMs(266) + 3600_000;
            Assert.AreEqual(266, DailyChallenge.DayIndex(ms));
            Assert.AreEqual(23 * 3600_000L, DailyChallenge.MsUntilNextDay(ms));
        }

        [Test]
        public void ScoreRanksLaterDaysFirstAndFasterTimesHigher()
        {
            long fastToday = DailyChallenge.EncodeScore(266, 61_230), slowToday = DailyChallenge.EncodeScore(266, 75_000);
            long fastYesterday = DailyChallenge.EncodeScore(265, 40_000);
            Assert.Greater(fastToday, slowToday);
            Assert.Greater(slowToday, fastYesterday);
            DailyChallenge.DecodeScore(fastToday, out int day, out long ms);
            Assert.AreEqual(266, day); Assert.AreEqual(61_230, ms);
            Assert.Less(DailyChallenge.EncodeScore(2000, 1), int.MaxValue, "fits a 32-bit leaderboard score for years");
        }

        [Test]
        public void StreakAndReward()
        {
            Assert.AreEqual(1, DailyChallenge.NextStreak(-100, 0, 10));
            Assert.AreEqual(4, DailyChallenge.NextStreak(9, 3, 10));
            Assert.AreEqual(3, DailyChallenge.NextStreak(10, 3, 10));
            Assert.AreEqual(1, DailyChallenge.NextStreak(8, 5, 10));
            Assert.AreEqual(150, DailyChallenge.FirstFinishReward(1));
            Assert.AreEqual(180, DailyChallenge.FirstFinishReward(2));
            Assert.AreEqual(330, DailyChallenge.FirstFinishReward(7));
            Assert.AreEqual(330, DailyChallenge.FirstFinishReward(30));
        }
    }
}
