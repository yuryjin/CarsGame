namespace RacingGame
{
    /// <summary>What the menu chose; read by RaceManager when a race starts.</summary>
    public static class RaceSetup
    {
        public static RaceEventDefinition Event;
        public static DifficultyTier Tier;
        public const float IntroTargetSpeed = 0.65f;
        public static bool IsIntroRace => Event != null && Event.eventId == "e1" && PlatformGlue.Save != null && PlatformGlue.Save.Data.Races == 0;
        public static string TierId => Tier != null ? Tier.tierId : "normal";
        /// <summary>Event board of the last event; the daily race has its own board (<see cref="DailyRace.BoardName"/>), so it shows e1's.</summary>
        public static string BoardName => Event != null && !DailyRace.IsDaily(Event) ? $"best_{Event.eventId}" : "best_e1";
    }
}
