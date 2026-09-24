using System;
using UnityEngine;
using RacingGame.Core.Daily;
using RacingGame.World;

namespace RacingGame
{
    /// <summary>
    /// Today's daily race as a runtime <see cref="RaceEventDefinition"/> (never saved as an asset). The day comes
    /// from the platform's server time when it has one, else the device clock (UTC), so a player cannot pick
    /// a day by changing the clock on Yandex.
    /// </summary>
    public static class DailyRace
    {
        public const string BoardName = "daily";
        /// <summary>Sky preset asset names by <see cref="DailySpec.Sky"/> index.</summary>
        private static readonly string[] SkyNames = { "Sky_Day", "Sky_Morning", "Sky_Sunset", "Sky_Overcast", "Sky_Night" };

        private static RaceEventDefinition cached;
        private static int cachedDay = int.MinValue;

        /// <summary>Server time if the platform provides it (<see cref="Core.Platform.IServerClock"/>), else the device clock.</summary>
        public static Func<long> UnixMsSource = () =>
        {
            long server = PlatformGlue.Platform is Core.Platform.IServerClock clock ? clock.ServerTimeUnixMs : 0;
            return server > 0 ? server : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        };

        public static long NowUnixMs => UnixMsSource();
        public static int Today => DailyChallenge.DayIndex(NowUnixMs);
        public static DailySpec TodaySpec => DailyChallenge.For(Today);

        public static bool IsDaily(RaceEventDefinition ev) => ev != null && ev.eventId != null && ev.eventId.Length > 1 && ev.eventId[0] == 'd' && char.IsDigit(ev.eventId[1]);

        /// <summary>Day of a daily event id ("d266" → 266), or −1.</summary>
        public static int DayOf(RaceEventDefinition ev) => IsDaily(ev) && int.TryParse(ev.eventId.Substring(1), out int d) ? d : -1;

        /// <summary>The event for today, built from the spec. Sky presets are borrowed from the authored events.</summary>
        public static RaceEventDefinition TodayEvent(RaceEventDefinition[] authored)
        {
            int day = Today;
            if (cached != null && cachedDay == day) return cached;
            var spec = DailyChallenge.For(day);
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            ev.name = "Daily_" + day;
            ev.hideFlags = HideFlags.DontSave;
            ev.eventId = spec.EventId;
            ev.nameKey = "daily.title";
            ev.mode = EventMode.Race;
            ev.laps = spec.Laps;
            ev.reversed = spec.Reversed;
            ev.sky = FindSky(authored, spec.Sky);
            ev.night = spec.Sky == DailyChallenge.SkyNight;
            ev.damageMultiplier = spec.DamageMultiplier;
            ev.pickupCount = spec.PickupCount;
            ev.twist = spec.Twist;
            ev.unlockAfterEventId = "";
            ev.order = -1;
            cached = ev; cachedDay = day;
            return ev;
        }

        private static SkyPreset FindSky(RaceEventDefinition[] authored, int index)
        {
            string wanted = SkyNames[Mathf.Clamp(index, 0, SkyNames.Length - 1)];
            if (authored != null)
                foreach (var e in authored)
                    if (e != null && e.sky != null && e.sky.name == wanted) return e.sky;
            return null;
        }
    }
}
