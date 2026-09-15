using System;

namespace RacingGame.Core.Sim
{
    /// <summary>
    /// Kills one car scored in one race, counted per victim so the payout can shrink for repeated
    /// kills of the same rival (see <c>Economy.RaceReward</c>). Pure data; no allocation per kill
    /// while the field fits the initial capacity.
    /// </summary>
    public sealed class KillTally
    {
        private int[] victimIds;
        private int[] counts;
        private int used;

        public KillTally(int capacity = 8)
        {
            victimIds = new int[Math.Max(1, capacity)];
            counts = new int[victimIds.Length];
        }

        public int Total { get; private set; }
        public int DistinctVictims => used;

        public void Clear() { used = 0; Total = 0; }

        public void Record(int victimId)
        {
            Total++;
            for (int i = 0; i < used; i++)
                if (victimIds[i] == victimId) { counts[i]++; return; }
            if (used == victimIds.Length)
            {
                Array.Resize(ref victimIds, used * 2);
                Array.Resize(ref counts, used * 2);
            }
            victimIds[used] = victimId;
            counts[used] = 1;
            used++;
        }

        public int CountFor(int victimId)
        {
            for (int i = 0; i < used; i++) if (victimIds[i] == victimId) return counts[i];
            return 0;
        }

        /// <summary>Kill counts per distinct victim, in first-kill order; null when there were no kills.</summary>
        public int[] ToPerVictimCounts()
        {
            if (used == 0) return null;
            var result = new int[used];
            Array.Copy(counts, result, used);
            return result;
        }
    }
}
