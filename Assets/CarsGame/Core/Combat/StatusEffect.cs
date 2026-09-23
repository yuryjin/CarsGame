namespace RacingGame.Core.Combat
{
    /// <summary>
    /// Configuration or description of a status effect payload.
    /// </summary>
    public struct StatusEffect
    {
        public StatusKind Kind;
        public int DurationTicks;
        public float Magnitude;

        public StatusEffect(StatusKind kind, int durationTicks, float magnitude = 1f)
        {
            Kind = kind;
            DurationTicks = durationTicks;
            Magnitude = magnitude;
        }
    }
}
