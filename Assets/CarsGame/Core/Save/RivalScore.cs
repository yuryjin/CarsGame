using System;
using System.Collections.Generic;

namespace RacingGame.Core.Save
{
    [Serializable] public class RivalScore
    {
        public string rival;
        public int wins, losses;

        public static (long wins, long losses) Total(ProgressData progress, string id)
        {
            long won = 0, lost = 0;
            if (progress?.ledger != null) foreach (var device in progress.ledger)
                if (device?.duels != null) foreach (var row in device.duels)
                    if (row != null && row.rival == id) { won += Math.Max(0, row.wins); lost += Math.Max(0, row.losses); }
            return (won, lost);
        }

        public static void Record(DeviceLedger device, string id, bool won)
        {
            if (string.IsNullOrEmpty(id)) return;
            device.duels ??= new List<RivalScore>();
            var row = device.duels.Find(x => x != null && x.rival == id);
            if (row == null) { row = new RivalScore { rival = id }; device.duels.Add(row); }
            if (won) { if (row.wins < int.MaxValue) row.wins++; }
            else if (row.losses < int.MaxValue) row.losses++;
        }

        public static List<RivalScore> Merge(List<RivalScore> a, List<RivalScore> b)
        {
            var scores = new SortedDictionary<string, RivalScore>(StringComparer.Ordinal);
            foreach (var list in new[] { a, b }) if (list != null) foreach (var row in list)
            {
                if (row == null || string.IsNullOrEmpty(row.rival)) continue;
                if (!scores.TryGetValue(row.rival, out var score)) scores[row.rival] = score = new RivalScore { rival = row.rival };
                score.wins = Math.Max(score.wins, row.wins); score.losses = Math.Max(score.losses, row.losses);
            }
            return new List<RivalScore>(scores.Values);
        }
    }
}
