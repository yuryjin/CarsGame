using System;
using System.Collections.Generic;
using RacingGame.Core.Economy;

namespace RacingGame.Core.Save
{
    [Serializable] public class BodyFinishCount
    {
        public string body;
        public int finishes;
    }

    [Serializable] public class FinishGap
    {
        public string eventId, rival;
        public long driverMs, rivalMs;
        public long Difference => Math.Abs(driverMs - rivalMs);
    }

    public static class DriverProfile
    {
        public static void Finish(DeviceLedger row, string body)
        {
            if (CarBodies.Index(body) < 0) return;
            row.bodyFinishes ??= new List<BodyFinishCount>();
            var count = row.bodyFinishes.Find(x => x != null && x.body == body);
            if (count == null) row.bodyFinishes.Add(new BodyFinishCount { body = body, finishes = 1 });
            else if (count.finishes < int.MaxValue) count.finishes++;
        }

        // One writer per device. Save snapshots with the same device ID carry overlapping counts.
        internal static List<BodyFinishCount> MergeCounts(List<BodyFinishCount> a, List<BodyFinishCount> b)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var list in new[] { a, b })
                if (list != null) foreach (var item in list)
                    if (item != null && CarBodies.Index(item.body) >= 0 && item.finishes > 0)
                        counts[item.body] = counts.TryGetValue(item.body, out int n) ? Math.Max(n, item.finishes) : item.finishes;
            var merged = new List<BodyFinishCount>(counts.Count);
            foreach (var item in counts) merged.Add(new BodyFinishCount { body = item.Key, finishes = item.Value });
            return merged;
        }

        public static string Favorite(ProgressData progress, out long finishes)
        {
            finishes = 0;
            string favorite = null;
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            if (progress?.ledger != null) foreach (var device in progress.ledger)
                if (device?.bodyFinishes != null) foreach (var item in device.bodyFinishes)
                    if (item != null && CarBodies.Index(item.body) >= 0 && item.finishes > 0)
                        counts[item.body] = counts.TryGetValue(item.body, out long n) ? n + item.finishes : item.finishes;
            foreach (var count in counts) if (count.Value > finishes) { favorite = count.Key; finishes = count.Value; }
            return favorite;
        }

        public static FinishGap Closest(FinishGap a, FinishGap b)
        {
            bool Valid(FinishGap gap) => gap != null && !string.IsNullOrEmpty(gap.eventId) && gap.driverMs > 0 && gap.rivalMs > 0;
            if (!Valid(b)) return Valid(a) ? a : null;
            if (!Valid(a)) return b;
            int order = a.Difference.CompareTo(b.Difference);
            if (order == 0) order = string.CompareOrdinal(a.eventId, b.eventId);
            if (order == 0) order = a.driverMs.CompareTo(b.driverMs);
            if (order == 0) order = a.rivalMs.CompareTo(b.rivalMs);
            if (order == 0) order = string.CompareOrdinal(a.rival, b.rival);
            return order <= 0 ? a : b;
        }
    }
}
