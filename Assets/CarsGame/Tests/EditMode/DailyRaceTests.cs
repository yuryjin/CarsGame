using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class DailyRaceTests
    {
        private System.Func<long> previousTimeSource;
        private RaceEventDefinition authored;
        private RaceEventDefinition daily;
        private RacingGame.World.SkyPreset sky;

        [SetUp]
        public void SetUp() { previousTimeSource = DailyRace.UnixMsSource; }

        [TearDown]
        public void TearDown()
        {
            DailyRace.UnixMsSource = previousTimeSource;
            if (daily != null) Object.DestroyImmediate(daily);
            if (authored != null) Object.DestroyImmediate(authored);
            if (sky != null) Object.DestroyImmediate(sky);
        }

        [Test]
        public void TodayEventBuildsTheDeterministicDailySpecAndBorrowsItsSky()
        {
            const int day = 2;
            DailyRace.UnixMsSource = () => RacingGame.Core.Daily.DailyChallenge.DayStartUnixMs(day) + 1234;
            var spec = RacingGame.Core.Daily.DailyChallenge.For(day);
            var skyNames = new[] { "Sky_Day", "Sky_Morning", "Sky_Sunset", "Sky_Overcast", "Sky_Night" };
            sky = ScriptableObject.CreateInstance<RacingGame.World.SkyPreset>();
            sky.name = skyNames[spec.Sky];
            authored = ScriptableObject.CreateInstance<RaceEventDefinition>();
            authored.sky = sky;

            daily = DailyRace.TodayEvent(new[] { authored });
            var repeated = DailyRace.TodayEvent(new[] { authored });

            Assert.AreSame(daily, repeated, "same UTC day reuses the built event");
            Assert.AreEqual(spec.EventId, daily.eventId);
            Assert.AreEqual(spec.Twist, daily.twist);
            Assert.AreEqual(spec.Laps, daily.laps);
            Assert.AreEqual(spec.Reversed, daily.reversed);
            Assert.AreEqual(spec.DamageMultiplier, daily.damageMultiplier);
            Assert.AreEqual(spec.PickupCount, daily.pickupCount);
            Assert.AreSame(sky, daily.sky);
            Assert.AreEqual(spec.Sky == RacingGame.Core.Daily.DailyChallenge.SkyNight, daily.night);
        }

        [Test]
        public void DailyIdParsingRejectsNonDailyAndMalformedIds()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            try
            {
                ev.eventId = "d271";
                Assert.IsTrue(DailyRace.IsDaily(ev));
                Assert.AreEqual(271, DailyRace.DayOf(ev));
                ev.eventId = "e271";
                Assert.IsFalse(DailyRace.IsDaily(ev));
                Assert.AreEqual(-1, DailyRace.DayOf(ev));
                ev.eventId = "d2x";
                Assert.IsTrue(DailyRace.IsDaily(ev));
                Assert.AreEqual(-1, DailyRace.DayOf(ev));
                Assert.IsFalse(DailyRace.IsDaily(null));
                Assert.AreEqual(-1, DailyRace.DayOf(null));
            }
            finally { Object.DestroyImmediate(ev); }
        }
    }
}
