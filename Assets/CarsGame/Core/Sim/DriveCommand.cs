namespace RacingGame.Core.Sim
{
    /// <summary>
    /// One tick of driver intent. This is the only way anything (player, AI, scripted test,
    /// later a network client) controls a car, so the simulation never touches input devices.
    /// Analog values are levels sampled at the tick; <see cref="ResetEdge"/> is an edge that a
    /// producer must deliver exactly once (see <see cref="EdgeLatch"/>).
    /// </summary>
    public struct DriveCommand
    {
        public int Tick;
        public float Throttle;   // -1..1 (negative = reverse / brake while moving forward)
        public float Steer;      // -1..1
        public bool Brake;       // handbrake / hard brake held
        public bool Fire;        // fire button held; the cannon's tick cooldown limits the rate
        public bool Secondary;   // secondary weapon (mines / freeze) trigger
        public bool UseItem;     // item / consumable trigger
        public bool ResetEdge;   // "put me back on the track" pressed since the last sample

        public static DriveCommand None(int tick) => new DriveCommand { Tick = tick };

        public DriveCommand Clamped()
        {
            var c = this;
            c.Throttle = c.Throttle < -1f ? -1f : (c.Throttle > 1f ? 1f : c.Throttle);
            c.Steer = c.Steer < -1f ? -1f : (c.Steer > 1f ? 1f : c.Steer);
            return c;
        }
    }
}
