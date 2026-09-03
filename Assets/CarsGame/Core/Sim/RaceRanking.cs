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

        /// <summary>Sorts in place and returns 1-based positions indexed by the entry's original index.</summary>
        public static int[] Rank(List<RankEntry> entries)
        {
            var order = new List<int>(entries.Count);
            for (int i = 0; i < entries.Count; i++) order.Add(i);
            order.Sort((x, y) => Instance.Compare(entries[x], entries[y]));
            var positions = new int[entries.Count];
            for (int p = 0; p < order.Count; p++) positions[order[p]] = p + 1;
            return positions;
        }
    }
}
