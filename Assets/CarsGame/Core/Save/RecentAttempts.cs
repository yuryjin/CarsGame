using System;
using System.Collections.Generic;

namespace RacingGame.Core.Save
{
    [Serializable]
    public sealed class RaceAttempt
    {
        public string id, eventId;
        public long finishedAt, timeMs;
        public RecordSetup setup;
    }

    public static class RecentAttempts
    {
        public const int Limit = 5;

        public static void Record(ProgressData progress, string id, string eventId, long timeMs, long finishedAt, RecordSetup setup)
        {
            var list = progress.recentAttempts ??= new List<RaceAttempt>();
            if (list.Count > 0) finishedAt = Math.Max(finishedAt, list[0].finishedAt + 1);
            Add(list, new RaceAttempt { id = id, eventId = eventId, timeMs = timeMs, finishedAt = finishedAt, setup = setup });
        }

        public static List<RaceAttempt> Merge(List<RaceAttempt> first, List<RaceAttempt> second = null)
        {
            var result = new List<RaceAttempt>(Limit + 1);
            if (first != null) foreach (var entry in first) Add(result, entry);
            if (second != null) foreach (var entry in second) Add(result, entry);
            return result;
        }

        public static RaceAttempt PreviousComparable(List<RaceAttempt> newestFirst, int index)
        {
            var current = newestFirst[index];
            for (int i = index + 1; i < newestFirst.Count; i++)
            {
                var older = newestFirst[i];
                if (older.eventId == current.eventId && RecordSetup.Same(older.setup, current.setup)) return older;
            }
            return null;
        }

        // Stable ordering also resolves duplicate IDs in diverged copies of the same device's save.
        private static int Compare(RaceAttempt a, RaceAttempt b)
        {
            int c = b.finishedAt.CompareTo(a.finishedAt); if (c != 0) return c;
            c = string.CompareOrdinal(a.id, b.id); if (c != 0) return c;
            c = string.CompareOrdinal(a.eventId, b.eventId); if (c != 0) return c;
            c = a.timeMs.CompareTo(b.timeMs); return c != 0 ? c : RecordSetup.Compare(a.setup, b.setup);
        }

        private static void Add(List<RaceAttempt> list, RaceAttempt entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || string.IsNullOrEmpty(entry.eventId)
                || entry.timeMs <= 0 || entry.finishedAt < 0 || entry.finishedAt == long.MaxValue) return;
            int duplicate = list.FindIndex(a => a.id == entry.id);
            if (duplicate >= 0)
            {
                if (Compare(list[duplicate], entry) <= 0) return;
                list.RemoveAt(duplicate);
            }
            list.Add(new RaceAttempt { id = entry.id, eventId = entry.eventId, timeMs = entry.timeMs,
                finishedAt = entry.finishedAt, setup = entry.setup?.Copy() });
            list.Sort(Compare);
            if (list.Count > Limit) list.RemoveAt(Limit);
        }
    }
}
