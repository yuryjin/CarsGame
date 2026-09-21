using System.Collections.Generic;

namespace RacingGame
{
    public sealed class RaceResultEntry
    {
        public string Name;
        public bool IsPlayer;
        public int Position;
        public bool Finished;
        public bool Survived;
        public double TotalTime;    // seconds from GO to the finish line (or race end)
        public double BestLap;      // seconds, +inf if none
        public int Laps;
        public int Kills;
        public int Deaths;
    }

    public sealed class RaceResults
    {
        public bool IsArena;
        public readonly List<RaceResultEntry> Entries = new List<RaceResultEntry>();
        public int LapsRequired;
        public double RaceDuration;
        public RaceResultEntry Player => Entries.Find(e => e.IsPlayer);
    }
}
