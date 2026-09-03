using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Wall-clock race time that ignores Time.timeScale and stops while paused (ads, host pause,
    /// pause menu). Lap and race times come from here so a browser hitch or a slow device
    /// (which the fixed-tick clock silently drops) cannot produce a faster lap.
    /// </summary>
    public static class RaceClock
    {
        public static bool Paused { get; set; }
        public static double Now { get; private set; }

        /// <summary>Advance by the frame's unscaled delta. Called once per frame by <see cref="SimulationDriver"/>.</summary>
        /// <summary>Longest single-frame step that counts as race time; a bigger gap is a load/hitch, not driving.</summary>
        public const float MaxFrameStep = 0.25f;

        public static void Advance(float unscaledDelta)
        {
            if (Paused || unscaledDelta <= 0f) return;
            Now += unscaledDelta > MaxFrameStep ? MaxFrameStep : unscaledDelta;
        }

        public static void Reset() { Now = 0d; Paused = false; }
    }
}
