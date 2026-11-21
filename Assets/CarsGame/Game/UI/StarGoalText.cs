namespace RacingGame
{
    public static class StarGoalText
    {
        public static string Description(RaceEventDefinition ev, int star)
        {
            if (ev == null) return "";
            if (ev.eventId == TrainMission.EventId && star < 2) return Loc.Get(star == 0 ? "train.star1" : "train.star2");
            if (ev.mode == EventMode.Mission && star < 2) return star == 0 ? Loc.Get("mission.star1") : Loc.Format("mission.star2", "n", ev.star2TargetKills > 0 ? ev.star2TargetKills : 2);
            if (star == 0) return Loc.Format("goals.place", "n", ev.star1TargetPosition > 0 ? ev.star1TargetPosition : 3);
            if (star == 1) return Loc.Format(ev.skillGoal == SkillGoal.Kills ? "goals.kills" : "goals.skill." + ev.skillGoal.ToString().ToLowerInvariant(),
                "n", ev.skillGoal == SkillGoal.Kills ? (ev.star2TargetKills > 0 ? ev.star2TargetKills : 2) : UnityEngine.Mathf.Max(1, ev.skillTarget));
            if (ev.finishGoal == FinishGoal.CleanRun) return Loc.Get("goals.clean_run");
            if (ev.finishGoal == FinishGoal.WinWithoutRepair) return Loc.Get("goals.no_repair");
            if (ev.finishGoal == FinishGoal.BeatTime) return Loc.Format("goals.time", "t", UiFactory.FormatTime(ev.targetSeconds));
            return Loc.Get(ev.star3ZeroDeaths ? "events.star3" : "goals.finish");
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
                    : star == 1 ? " (" + ev.SkillCount(entry) + "/" + (ev.skillGoal == SkillGoal.Kills ? (ev.star2TargetKills > 0 ? ev.star2TargetKills : 2) : UnityEngine.Mathf.Max(1, ev.skillTarget)) + ")"
                    : ev.finishGoal == FinishGoal.BeatTime ? " (" + UiFactory.FormatTime(entry.TotalTime) + ")"
                    : ev.finishGoal == FinishGoal.CleanRun ? ""
                    : " (" + (ev.finishGoal == FinishGoal.WinWithoutRepair ? entry.RepairsUsed : entry.Deaths) + ")";
                text += (star == 0 ? "" : "\n") + "<color=" + color + ">★ " + Description(ev, star) + counter + "</color>";
            }
            return text;
        }
    }
}
