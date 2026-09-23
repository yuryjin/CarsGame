using System;

namespace RacingGame.Core.Sim
{
    /// <summary>
    /// Top-speed multiplier for an AI car from its gap to the player. Asymmetric on purpose: a car far
    /// behind may gain up to <see cref="MaxCatchUp"/>, a car far ahead gives up at most
    /// <see cref="MaxHoldBack"/> and nothing on its last lap (a leader that visibly slows before the
    /// line reads as cheating), and the first seconds of a race run without any help so the start is honest.
    /// </summary>
    public static class RubberBand
    {
        public const float MaxCatchUp = 0.15f;
        public const float MaxHoldBack = 0.08f;
        public const float GapScaleMetres = 150f;
        public const float WarmupSeconds = 20f;

        /// <param name="gapMetres">AI progress minus player progress (+ = AI ahead).</param>
        /// <param name="aiOnLastLap">The AI car is on its final lap.</param>
        /// <param name="raceSeconds">Time since GO.</param>
        /// <param name="strength">Difficulty scale, 0 = off, 1 = full range.</param>
        public static float Multiplier(float gapMetres, bool aiOnLastLap, double raceSeconds, float strength)
        {
            if (strength <= 0f || raceSeconds < WarmupSeconds || float.IsNaN(gapMetres)) return 1f;
            float x = -gapMetres / GapScaleMetres;                       // + = AI behind the player
            float raw = x > 0f ? Math.Min(x, MaxCatchUp) : Math.Max(x, -MaxHoldBack);
            if (raw < 0f && aiOnLastLap) raw = 0f;
            return 1f + raw * Math.Min(1f, strength);
        }
    }
}
