namespace RacingGame.Core.Combat
{
    /// <summary>
    /// Kinds of status effects that can affect a vehicle during racing/combat.
    /// </summary>
    public enum StatusKind : byte
    {
        None = 0,
        Frozen = 1,
        Jammed = 2,
        Punctured = 3,
        Shield = 4,
        Slowed = 5
    }
}
