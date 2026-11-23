namespace RacingGame
{
    public sealed partial class GameFlow
    {
        private RaceEventDefinition yesterdayPractice, beforeYesterdayPractice;
        public int LastYesterdayDay { get; private set; } = int.MinValue;
        public long LastYesterdayTimeMs { get; private set; }

        public void StartYesterdayPractice()
        {
            if (State != FlowState.Menu || !PlatformGlue.IsReady || PlatformGlue.Save.LocalVersionTooNew) return;
            yesterdayPractice = DailyRace.YesterdayEvent(events);
            beforeYesterdayPractice = RaceSetup.Event;
            StartRace(yesterdayPractice, tierBeforeDaily != null ? tierBeforeDaily : RaceSetup.Tier);
        }

        private void EndYesterdayPractice(RaceResultEntry player)
        {
            if (player != null && player.Finished)
            {
                LastYesterdayDay = DailyRace.DayOf(yesterdayPractice);
                LastYesterdayTimeMs = (long)(player.TotalTime * 1000d);
            }
            EnterMenu();
        }

        private void RestoreYesterdayPractice()
        {
            if (yesterdayPractice == null) return;
            PlatformGlue.Platform?.SetGameplayActive(false);
            RaceSetup.Event = beforeYesterdayPractice;
            Destroy(yesterdayPractice);
            yesterdayPractice = beforeYesterdayPractice = null;
        }
    }
}
