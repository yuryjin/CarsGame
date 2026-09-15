using System.Collections.Generic;

namespace RacingGame.Core.Sim
{
    /// <summary>Snapshot of one participant used for ranking. Pure data, no Unity types.</summary>
    public struct RankEntry
    {
        public int CarId;
        public bool Finished;
        public double FinishTime;     // seconds, valid when Finished
        public double TotalProgress;  // lap * trackLength + distance, valid when !Finished
    }

    /// <summary>
    /// Total order for race positions: everyone who finished comes first, ordered by finish
    /// time ascending; then everyone still racing, ordered by progress descending; CarId
    /// breaks ties so the order is stable and deterministic.
    /// </summary>
    public sealed class RaceRanking : IComparer<RankEntry>
    {
        public static readonly RaceRanking Instance = new RaceRanking();

        public int Compare(RankEntry a, RankEntry b)
        {
            if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
            int c;
            if (a.Finished)
                c = a.FinishTime.CompareTo(b.FinishTime);
            else
                c = b.TotalProgress.CompareTo(a.TotalProgress);
            return c != 0 ? c : a.CarId.CompareTo(b.CarId);
        }

        /// <summary>Returns 1-based positions indexed by the entry's original index. Allocates; tests and tools only.</summary>
        public static int[] Rank(List<RankEntry> entries)
        {
            var order = new List<int>(entries.Count);
            var positions = new int[entries.Count];
            Rank(entries, order, positions);
            return positions;
        }

        /// <summary>
        /// Allocation-free ranking for the race loop: <paramref name="order"/> is scratch space,
        /// <paramref name="positions"/> (length ≥ entries.Count) receives 1-based positions by original index.
        /// Insertion sort: the field is at most 8 cars and nearly sorted between calls.
        /// </summary>
        public static void Rank(List<RankEntry> entries, List<int> order, int[] positions)
        {
            order.Clear();
            for (int i = 0; i < entries.Count; i++) order.Add(i);
            for (int i = 1; i < order.Count; i++)
            {
                int key = order[i];
                int j = i - 1;
                while (j >= 0 && Instance.Compare(entries[order[j]], entries[key]) > 0) { order[j + 1] = order[j]; j--; }
                order[j + 1] = key;
            }
            for (int p = 0; p < order.Count; p++) positions[order[p]] = p + 1;
        }
    }
}
