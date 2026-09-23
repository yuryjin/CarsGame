namespace RacingGame.Core.Combat
{
    /// <summary>
    /// Outcome of processing an incoming hit by CarHealth.ApplyHit.
    /// </summary>
    public struct HitResult
    {
        public bool DamageApplied;
        public float DamageDealt;
        public bool ShieldAbsorbed;
        public bool Fatal;
        public bool StatusApplied;

        public static HitResult Rejected => new HitResult { DamageApplied = false };
    }
}
