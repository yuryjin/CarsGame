namespace RacingGame.Core.Sim
{
    /// <summary>
    /// Bridges a frame-rate "pressed this frame" event to a fixed-rate simulation: a press
    /// recorded in Update survives until the next simulation sample and is consumed exactly
    /// once, no matter whether zero or several fixed ticks run between two frames.
    /// </summary>
    public sealed class EdgeLatch
    {
        private int pending;

        /// <summary>Record one press (called from the variable-rate side).</summary>
        public void Press() { if (pending < int.MaxValue) pending++; }

        /// <summary>True if at least one press is waiting.</summary>
        public bool HasPending => pending > 0;

        /// <summary>Consume one press. Returns true exactly once per recorded press.</summary>
        public bool Consume()
        {
            if (pending <= 0) return false;
            pending--;
            return true;
        }

        /// <summary>Consume every waiting press as a single edge (double taps collapse).</summary>
        public bool ConsumeAll()
        {
            bool any = pending > 0;
            pending = 0;
            return any;
        }

        public void Clear() { pending = 0; }
    }
}
