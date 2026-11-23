using System;
using System.Globalization;

namespace RacingGame
{
    public static class StarGoalText
    {
        public static string Description(RaceEventDefinition ev, int star)
        {
            if (ev == null) return "";
            if (ev.eventId == TrainMission.EventId && star < 2) return Loc.Get(star == 0 ? "train.star1" : "train.star2");
            if (ev.mode == EventMode.Mission && star < 2) return star == 0 ? Loc.Get("mission.star1") : Loc.Format("mission.star2", "n", ev.RequiredSkillCount);
            if (star == 0) return Loc.Format("goals.place", "n", ev.RequiredPosition);
            if (star == 1) return Loc.Format(ev.skillGoal == SkillGoal.Kills ? "goals.kills" : "goals.skill." + ev.skillGoal.ToString().ToLowerInvariant(),
                "n", ev.RequiredSkillCount);
            if (ev.finishGoal == FinishGoal.CleanRun) return Loc.Get("goals.clean_run");
            if (ev.finishGoal == FinishGoal.WinWithoutRepair) return Loc.Get("goals.no_repair");
            if (ev.finishGoal == FinishGoal.BeatTime) return Loc.Format("goals.time", "t", UiFactory.FormatTime(ev.targetSeconds));
            return Loc.Get(ev.star3ZeroDeaths ? "events.star3" : "goals.finish");
        }

        public static string MissedProgress(RaceEventDefinition ev, RaceResultEntry entry, int star)
        {
            if (ev == null || entry == null) return "";
            if (star == 1) return Loc.Format("results.goal.count", "n", ev.SkillCount(entry), "target", ev.RequiredSkillCount);
            if (!entry.Finished) return Loc.Get("results.dnf");
            if (star == 0) return Loc.Format("results.goal.places", "n", entry.Position - ev.RequiredPosition);
            switch (ev.finishGoal)
            {
                case FinishGoal.BeatTime:
                    double late = entry.TotalTime - ev.targetSeconds;
                    if (!(late > 0) || double.IsInfinity(late)) return "";
                    string seconds = Math.Max(0.001, Math.Round(late, 3, MidpointRounding.AwayFromZero))
                        .ToString("0.000", CultureInfo.GetCultureInfo(Loc.Language));
                    return Loc.Format("results.goal.late", "t", seconds);
                case FinishGoal.CleanRun:
                    return Loc.Format("results.goal.clean", "wrecks", entry.Deaths, "resets", entry.Resets);
                case FinishGoal.WinWithoutRepair:
                    return Loc.Format("results.goal.win_clean", "p", entry.Position, "repairs", entry.RepairsUsed);
                default:
                    return Loc.Format("results.goal.wrecks", "n", entry.Deaths);
            }
        }

        public static string Progress(RaceEventDefinition ev, RaceResultEntry entry)
        {
            int mask = ev.StarMask(entry);
            string text = "";
            for (int star = 0; star < 3; star++)
            {
                bool failed = star == 2 && (ev.finishGoal == FinishGoal.WinWithoutRepair ? entry.RepairsUsed > 0
                    : ev.finishGoal == FinishGoal.BeatTime ? entry.TotalTime > ev.targetSeconds
                    : ev.finishGoal == FinishGoal.CleanRun ? entry.Deaths > 0 || entry.Resets > 0
                    : ev.star3ZeroDeaths && entry.Deaths > 0);
                string color = (mask & (1 << star)) != 0 ? "#FFCC40" : failed ? "#FF7777" : "#DCE5EF";
                string counter = star == 0 ? (ev.mode == EventMode.Mission ? "" : " (" + entry.Position + ")")
                    : star == 1 ? " (" + ev.SkillCount(entry) + "/" + ev.RequiredSkillCount + ")"
                    : ev.finishGoal == FinishGoal.BeatTime ? " (" + UiFactory.FormatTime(entry.TotalTime) + ")"
                    : ev.finishGoal == FinishGoal.CleanRun ? ""
                    : " (" + (ev.finishGoal == FinishGoal.WinWithoutRepair ? entry.RepairsUsed : entry.Deaths) + ")";
                text += (star == 0 ? "" : "\n") + "<color=" + color + ">★ " + Description(ev, star) + counter + "</color>";
            }
            return text;
        }
    }
}
