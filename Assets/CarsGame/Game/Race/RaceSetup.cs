namespace RacingGame
{
    /// <summary>What the menu chose; read by RaceManager when a race starts.</summary>
    public static class RaceSetup
    {
        public static RaceEventDefinition Event;
        public static DifficultyTier Tier;
        public static string TierId => Tier != null ? Tier.tierId : "normal";
        public static string BoardName => Event != null ? $"best_{Event.eventId}" : "best_e1";
    }
}
