using System;

namespace RacingGame.Core.Track
{
    /// <summary>
    /// Lap validation over an ordered ring of virtual gates. Pure logic, fed with the car's
    /// distance along the loop each tick. A gate is credited only when crossed forward, in
    /// order, with a plausible per-tick movement; a lap completes when gate 0 is crossed again
    /// after all others. Teleports and reverse driving never credit anything.
    /// </summary>
    public sealed class LapCounter
    {
        public float TrackLength { get; }
        public float StartLine { get; }
        public int GateCount { get; }
        public float MaxDeltaPerTick { get; }

        public int LapsCompleted { get; private set; }
        public int NextGate { get; private set; }
        public bool LapJustCompleted { get; private set; }
        /// <summary>True once every intermediate gate of the current lap was passed and only the line remains.</summary>
        public bool LineArmed { get; private set; }

        private float prevDistance;
        private int suppressTicks;
        private bool initialised;

        public LapCounter(float trackLength, float startLine, int gateCount, float maxDeltaPerTick)
        {
            if (trackLength <= 0f) throw new ArgumentException("trackLength");
            if (gateCount < 2) throw new ArgumentException("gateCount");
            TrackLength = trackLength;
            StartLine = Wrap(startLine);
            GateCount = gateCount;
            MaxDeltaPerTick = maxDeltaPerTick;
        }

        public float Wrap(float d)
        {
            d %= TrackLength;
            return d < 0f ? d + TrackLength : d;
        }

        public float SignedDelta(float from, float to)
        {
            float d = Wrap(to - from);
            return d > TrackLength * 0.5f ? d - TrackLength : d;
        }

        public float GateDistance(int gate) => Wrap(StartLine + gate * (TrackLength / GateCount));

        /// <summary>Distance from the start line in [0, TrackLength).</summary>
        public float FromStart(float d) => Wrap(d - StartLine);

        /// <summary>
        /// Lap-aware progress used for ranking. A car that still has gate 0 (the line) ahead of
        /// it and sits in the second half of the loop is on the tail of the previous lap — e.g.
        /// the grid before the start — so it ranks below anyone who already crossed the line.
        /// </summary>
        public float TotalProgress(float d)
        {
            float fromStart = FromStart(d);
            int lap = LapsCompleted;
            if (NextGate == 0 && !LineArmed && fromStart > TrackLength * 0.5f) lap -= 1;
            return lap * TrackLength + fromStart;
        }

        /// <summary>Reset for a new race from the given position. Cars behind the line start with gate 0 pending.</summary>
        public void Begin(float distance)
        {
            LapsCompleted = 0;
            LapJustCompleted = false;
            LineArmed = false;
            prevDistance = Wrap(distance);
            initialised = true;
            suppressTicks = 0;
            NextGate = FromStart(prevDistance) > TrackLength * 0.5f ? 0 : 1;
        }

        /// <summary>Re-project after a teleport: the jump is not scored.</summary>
        public void Resync(float distance)
        {
            prevDistance = Wrap(distance);
            suppressTicks = 2;
            initialised = true;
        }

        public void ApplyStanding(float distance, int laps, int gate, bool armed)
        {
            if (laps < 0 || gate < 0 || gate >= GateCount || (armed && gate != 0)) return;
            Resync(distance);
            LapsCompleted = laps;
            NextGate = gate;
            LineArmed = armed;
            LapJustCompleted = false;
        }

        /// <summary>Feed the distance for this tick. Returns the signed movement along the track (m).</summary>
        public float Step(float distance)
        {
            LapJustCompleted = false;
            distance = Wrap(distance);
            if (!initialised) { Begin(distance); return 0f; }

            float delta = SignedDelta(prevDistance, distance);
            bool teleport = Math.Abs(delta) > MaxDeltaPerTick;
            if (suppressTicks > 0 || teleport)
            {
                if (suppressTicks > 0) suppressTicks--;
                prevDistance = distance;
                return delta;
            }

            if (delta > 0f)
            {
                float toGate = SignedDelta(prevDistance, GateDistance(NextGate));
                if (toGate >= 0f && toGate <= delta)
                {
                    if (NextGate == 0)
                    {
                        // Crossing the line: a lap completes only if every other gate was passed first.
                        if (LineArmed) { LapsCompleted++; LapJustCompleted = true; LineArmed = false; }
                        NextGate = 1;
                    }
                    else
                    {
                        NextGate++;
                        if (NextGate >= GateCount) { NextGate = 0; LineArmed = true; }
                    }
                }
            }

            prevDistance = distance;
            return delta;
        }
    }
}
