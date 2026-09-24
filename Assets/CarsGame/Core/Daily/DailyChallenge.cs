using System;

namespace RacingGame.Core.Daily
{
    /// <summary>Rule twist that makes one daily race (and some fixed events) different from the base race.</summary>
    public enum RaceTwist
    {
        None = 0,
        /// <summary>Cannons off; everyone carries mines, ammo pickups refill them.</summary>
        MinesOnly = 1,
        /// <summary>All damage ×2.</summary>
        DoubleDamage = 2,
        /// <summary>Night sky with headlights.</summary>
        Night = 3,
        /// <summary>No respawn: a wreck is out of the race (the player may continue once with a rewarded ad).</summary>
        OneLife = 4,
        /// <summary>Twice the pickups on the track.</summary>
        Arsenal = 5,
        /// <summary>Two short laps.</summary>
        Sprint = 6,
    }

    /// <summary>Everything that defines one day's race. Identical for every player on the same UTC day.</summary>
    public readonly struct DailySpec
    {
        public readonly int Day;
        public readonly RaceTwist Twist;
        public readonly bool Reversed;
        /// <summary>Index into the sky preset list (0 day, 1 morning, 2 sunset, 3 overcast, 4 night).</summary>
        public readonly int Sky;
        public readonly int Laps;
        public readonly float DamageMultiplier;
        public readonly int PickupCount;
        /// <summary>Seed for pickups and AI streams, so the race itself repeats for everyone that day.</summary>
        public readonly int Seed;

        public DailySpec(int day, RaceTwist twist, bool reversed, int sky, int laps, float damage, int pickups, int seed)
        {
            Day = day; Twist = twist; Reversed = reversed; Sky = sky; Laps = laps; DamageMultiplier = damage; PickupCount = pickups; Seed = seed;
        }

        /// <summary>Event id used for bests, ghosts and the daily seed ("d271").</summary>
        public string EventId => "d" + Day;
    }

    /// <summary>
    /// The daily race: a deterministic spec per UTC day, the leaderboard score encoding, the streak and
    /// the once-a-day reward. Pure; the day comes from the caller (server time when the platform has it).
    /// </summary>
    public static class DailyChallenge
    {
        public const int SkyCount = 5;
        public const int SkyNight = 4;
        /// <summary>Day 0 = 2026-01-01 UTC.</summary>
        public static readonly DateTime Epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly RaceTwist[] TwistCycle = { RaceTwist.MinesOnly, RaceTwist.DoubleDamage, RaceTwist.Night, RaceTwist.OneLife, RaceTwist.Arsenal, RaceTwist.Sprint };

        public static int DayIndex(long unixMs) => (int)Math.Floor((unixMs - (Epoch - DateTime.UnixEpoch).TotalMilliseconds) / 86400000d);
        public static int DayIndex(DateTime utc) => (int)Math.Floor((utc.ToUniversalTime() - Epoch).TotalDays);
        public static long DayStartUnixMs(int day) => (long)(Epoch - DateTime.UnixEpoch).TotalMilliseconds + day * 86400000L;

        /// <summary>Milliseconds until the next day's race (for the "new race in 5 h 12 min" line).</summary>
        public static long MsUntilNextDay(long unixMs) => DayStartUnixMs(DayIndex(unixMs) + 1) - unixMs;

        public static DailySpec For(int day)
        {
            uint h = Hash((uint)day);
            int week = FloorDiv(day, TwistCycle.Length);
            var twist = WeekOrder(week)[day - week * TwistCycle.Length];

            bool reversed = (Hash(h ^ 0xA5A5A5A5u) & 1) != 0;
            int sky = twist == RaceTwist.Night ? SkyNight : (int)(Hash(h ^ 0x5bd1e995u) % (SkyCount - 1));   // night only as its own twist
            int laps = twist == RaceTwist.Sprint ? 2 : 3;
            float damage = twist == RaceTwist.DoubleDamage ? 2f : 1f;
            int pickups = twist == RaceTwist.Arsenal ? 18 : 9;
            int seed = (int)(Hash(h ^ 0x27d4eb2fu) & 0x7fffffff);
            return new DailySpec(day, twist, reversed, sky, laps, damage, pickups, seed);
        }

        // Six twists in a shuffled order per week: every twist once a week, never the same on two days in a
        // row. Only slots 0 and 1 are ever swapped, so the previous week's last slot is final.
        private static RaceTwist[] WeekOrder(int week)
        {
            var order = Shuffled(week);
            if (order[0] == Shuffled(week - 1)[TwistCycle.Length - 1]) (order[0], order[1]) = (order[1], order[0]);
            return order;
        }

        private static RaceTwist[] Shuffled(int week)
        {
            var order = (RaceTwist[])TwistCycle.Clone();
            uint wh = Hash((uint)week ^ 0x9E3779B9u);
            for (int i = order.Length - 1; i > 0; i--)
            {
                wh = Hash(wh);
                int j = (int)(wh % (uint)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        // ---------- leaderboard ----------
        // Yandex keeps one score per player and needs a fixed sort order, and boards never reset. The daily
        // board is therefore sorted DESCENDING and the score puts the day in the high digits: any run of a
        // later day outranks every older run, and within a day a faster time scores higher. Readers keep
        // only the rows of today.
        public const int TimeSlots = 1000000;          // centiseconds; 9999.99 s max

        public static long EncodeScore(int day, long timeMs)
        {
            long cs = Math.Clamp((timeMs + 5) / 10, 0, TimeSlots - 1);
            return (long)day * TimeSlots + (TimeSlots - 1 - cs);
        }

        public static void DecodeScore(long score, out int day, out long timeMs)
        {
            day = (int)(score / TimeSlots);
            long cs = TimeSlots - 1 - score % TimeSlots;
            timeMs = cs * 10;
        }

        // ---------- streak & reward ----------

        /// <summary>Consecutive finished days ending at <paramref name="day"/> (0 when that day is not in the set).</summary>
        public static int StreakEndingAt(System.Collections.Generic.ICollection<int> finishDays, int day)
        {
            if (finishDays == null) return 0;
            int n = 0;
            while (finishDays.Contains(day - n)) n++;
            return n;
        }

        /// <summary>The streak shown before today's race: today's if already finished, else yesterday's (still alive).</summary>
        public static int CurrentStreak(System.Collections.Generic.ICollection<int> finishDays, int today) =>
            Math.Max(StreakEndingAt(finishDays, today), StreakEndingAt(finishDays, today - 1));

        public const int BaseReward = 150;
        public const int StreakStep = 30;
        public const int StreakCap = 7;

        /// <summary>Bonus for the first finish of the day: 150, 180 … 330 from day 7 of a streak.</summary>
        public static int FirstFinishReward(int streak) => BaseReward + StreakStep * (Math.Clamp(streak, 1, StreakCap) - 1);

        // ---------- helpers ----------
        private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

        /// <summary>Stable integer hash (lowbias32); identical on every runtime, unlike string.GetHashCode or System.Random.</summary>
        public static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU;
            x ^= x >> 15; x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }
    }
}
