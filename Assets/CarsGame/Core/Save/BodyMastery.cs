using System;
using RacingGame.Core.Economy;

namespace RacingGame.Core.Save
{
    public static class BodyMastery
    {
        public const int BadgeAt = 3;
        public static long Count(ProgressData progress, BodyClass body)
        {
            long total = 0;
            if (progress?.ledger != null) foreach (var row in progress.ledger) total += Level(row?.mastery, (int)body);
            return total;
        }
        public static void Record(DeviceLedger row, BodyClass body, int shortcuts, int driftBoosts, int kills, int bombPasses)
        {
            int i = (int)body;
            if (i < 0 || i > 3) return;
            int amount = body == BodyClass.Buggy ? shortcuts : body == BodyClass.Sport ? driftBoosts : body == BodyClass.Armored ? kills : bombPasses;
            if (amount <= 0) return;
            if (row.mastery == null || row.mastery.Length < 4) Array.Resize(ref row.mastery, 4);
            row.mastery[i] = (int)Math.Min(int.MaxValue, (long)Level(row.mastery, i) + amount);
        }
        internal static int Level(int[] values, int i) => values != null && i >= 0 && i < values.Length ? Math.Max(0, values[i]) : 0;
        internal static int[] Merge(int[] a, int[] b)
        {
            var result = new int[4];
            for (int i = 0; i < result.Length; i++) result[i] = Math.Max(Level(a, i), Level(b, i));
            return result;
        }
    }
}
