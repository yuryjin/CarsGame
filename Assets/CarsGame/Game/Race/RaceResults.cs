using System.Collections.Generic;

namespace RacingGame
{
    public sealed class RaceResultEntry
    {
        public string Name, RivalId;
        public bool IsPlayer;
        public int Position;
        public bool Finished;
        // An unfinished car may still be racing when the player opens the results.
        public bool Eliminated;
        public bool Survived;
        public double TotalTime;    // seconds from GO to the finish line (or race end)
        public double BestLap;      // seconds, +inf if none
        public int Laps;
        public int Kills;
        // Captured for payout at the finish (or race end for unfinished/arena participants).
        // HUD snapshots before that point do not allocate a per-victim array.
        public int[] KillsPerVictim;
        public int TurretPoints;
        public int Deaths;
        public int CarId, Assists;
        public Core.Sim.ArenaPoints ArenaPoints;
        public int RepairsUsed;
        public int DriftBoosts, Shortcuts, CeilingBoosts, BombPasses, Resets, Rams;
        public bool CleanFinalLap;
    }

    internal struct RecentHit
    {
        public string driver;
        public bool isPlayer, mine, environmental, fatal, shield;
        public int damage;
    }

    public sealed class RaceResults
    {
        public bool IsArena;
        public readonly List<RaceResultEntry> Entries = new List<RaceResultEntry>();
        internal RecentHit[] LastHits;
        public int LapsRequired;
        public double RaceDuration;
        public RaceResultEntry Player => Entries.Find(e => e.IsPlayer);
    }
}
