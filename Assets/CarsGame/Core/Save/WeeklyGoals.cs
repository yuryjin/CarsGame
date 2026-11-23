using System;
using System.Collections.Generic;
using RacingGame.Core.Daily;

namespace RacingGame.Core.Save
{
    public enum WeeklyDiscipline { Circuit, Arena, Mission }

    [Serializable]
    public sealed class WeeklyGoalState
    {
        public int week, mask;
    }

    public static class WeeklyGoals
    {
        // The epoch starts on a Thursday. Weeks change on Monday at 00:00 UTC.
        public static int Week(long now) => (int)Math.Floor((DailyChallenge.DayIndex(now) + 3d) / 7d);
        public static string Reward(int week) => (week & 1) == 0 ? "paint:steel" : "livery:hood";
        public static int Mask(ProgressData p, int week) => p.weekly?.Find(w => w.week == week)?.mask ?? 0;
        public static string Key(int week, WeeklyDiscipline discipline)
        {
            bool odd = (week & 1) != 0;
            switch (discipline)
            {
                case WeeklyDiscipline.Circuit: return odd ? "weekly.podium" : "weekly.race";
                case WeeklyDiscipline.Arena: return odd ? "weekly.arena" : "weekly.kill";
                default: return odd ? "weekly.clean" : "weekly.mission";
            }
        }

        public static bool Record(ProgressData p, long now, WeeklyDiscipline discipline, bool finished, int position, int kills, int deaths)
        {
            int week = Week(now); bool odd = (week & 1) != 0;
            if (!finished || discipline == WeeklyDiscipline.Circuit && odd && (position < 1 || position > 3)
                || discipline == WeeklyDiscipline.Arena && !odd && kills < 1
                || discipline == WeeklyDiscipline.Mission && odd && deaths != 0) return false;
            var rows = p.weekly ??= new List<WeeklyGoalState>();
            int bit = 1 << (int)discipline;
            var row = rows.Find(w => w.week == week);
            if (row == null) { row = new WeeklyGoalState { week = week }; rows.Add(row); }
            if ((row.mask & bit) != 0) return false;
            row.mask |= bit; Trim(rows);
            return Grant(p, row);
        }

        public static void MergeInto(ProgressData p, List<WeeklyGoalState> first, List<WeeklyGoalState> second = null)
        {
            p.weekly = new List<WeeklyGoalState>();
            foreach (var list in new[] {first, second})
                if (list != null) foreach (var w in list)
                {
                    if (w == null || (w.mask & 7) == 0) continue;
                    var row = p.weekly.Find(a => a.week == w.week);
                    if (row == null) { row = new WeeklyGoalState { week = w.week }; p.weekly.Add(row); }
                    row.mask |= w.mask & 7;
                    Grant(p, row);
                }
            Trim(p.weekly);
        }

        private static bool Grant(ProgressData p, WeeklyGoalState row)
        {
            if (row.mask != 7) return false;
            p.cosmetics ??= new List<string>();
            string reward = Reward(row.week);
            if (p.cosmetics.Contains(reward)) return false;
            p.cosmetics.Add(reward);
            return true;
        }
        private static void Trim(List<WeeklyGoalState> rows)
        {
            rows.Sort((a,b) => b.week.CompareTo(a.week));
            if (rows.Count > 8) rows.RemoveRange(8, rows.Count - 8);
        }
    }
}
