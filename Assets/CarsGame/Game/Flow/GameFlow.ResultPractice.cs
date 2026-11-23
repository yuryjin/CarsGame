namespace RacingGame
{
    public sealed partial class GameFlow
    {
        private RaceResults practiceReturnResults;

        internal RaceResults ResultsForMenu => practiceReturnResults ?? race.Results;
        internal bool ReturningToResults => State == FlowState.Results && practiceReturnResults != null;

        internal PracticeLesson MissedLesson
        {
            get
            {
                var ev = CurrentEvent;
                var result = ResultsForMenu?.Player;
                if (State != FlowState.Results || ev == null || result == null || ev.mode == EventMode.Mission
                    || DailyRace.IsDaily(ev) || ev.SkillCount(result) >= ev.RequiredSkillCount) return PracticeLesson.None;
                switch (ev.skillGoal)
                {
                    case SkillGoal.DriftBoosts: return PracticeLesson.Drift;
                    case SkillGoal.Shortcuts: return PracticeLesson.Shortcut;
                    case SkillGoal.CeilingBoost: return PracticeLesson.WallRide;
                    case SkillGoal.Kills when ev.twist != Core.Daily.RaceTwist.MinesOnly
                        && ev.twist != Core.Daily.RaceTwist.HotBomb: return PracticeLesson.Aim;
                    default: return PracticeLesson.None;
                }
            }
        }
    }
}
