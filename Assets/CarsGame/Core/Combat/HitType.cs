namespace RacingGame.Core.Combat
{
    /// <summary>
    /// Classification of hit sources for damage filtering, stats, and effects.
    /// </summary>
    public enum HitType : byte
    {
        None = 0,
        Projectile = 1,
        Collision = 2,
        Hazard = 3,
        Explosion = 4,
        Direct = 5
    }
}
