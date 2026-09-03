using System;
using UnityEngine;
using RacingGame.Core.Platform;

namespace RacingGame
{
    /// <summary>
    /// Idempotent, multi-source pause: each source sets or clears its own bit; the game is paused
    /// while any bit is set. Time.timeScale, the race clock and the audio listener follow.
    /// </summary>
    public static class PauseController
    {
        public static PauseReason Reasons { get; private set; }
        public static bool IsPaused => Reasons != PauseReason.None;
        public static event Action<bool> PausedChanged;

        public static void Set(PauseReason reason, bool paused)
        {
            var before = Reasons;
            Reasons = paused ? (Reasons | reason) : (Reasons & ~reason);
            bool was = before != PauseReason.None, now = Reasons != PauseReason.None;
            if (was == now) return;
            Time.timeScale = now ? 0f : 1f;
            RaceClock.Paused = now;
            AudioListener.pause = now;
            PausedChanged?.Invoke(now);
        }

        public static void Toggle(PauseReason reason) => Set(reason, (Reasons & reason) == 0);

        /// <summary>Clear everything (scene change, race restart).</summary>
        public static void ResetAll()
        {
            Reasons = PauseReason.None;
            Time.timeScale = 1f;
            RaceClock.Paused = false;
            AudioListener.pause = false;
        }
    }
}
