using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Unscaled frame time, stopped by pause and bounded during long stalls. Its cap must never
    /// be smaller than Unity's physics catch-up budget: otherwise a slow device can advance
    /// more driving time than the lap timer records.
    /// </summary>
    public static class RaceClock
    {
        public static bool Paused { get; set; }
        public static double Now { get; private set; }

        /// <summary>Discard very long load gaps, but count at least as much time as physics can simulate.</summary>
        public static float MaxFrameStep => Mathf.Max(0.25f, Time.maximumDeltaTime);

        /// <summary>Advance once per rendered frame from <see cref="SimulationDriver"/>.</summary>
        public static void Advance(float unscaledDelta)
        {
            if (Paused || unscaledDelta <= 0f) return;
            Now += unscaledDelta > MaxFrameStep ? MaxFrameStep : unscaledDelta;
        }

        public static void Reset() { Now = 0d; Paused = false; }
    }
}
