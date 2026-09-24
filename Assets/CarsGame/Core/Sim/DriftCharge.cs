using System;

namespace RacingGame.Core.Sim
{
    /// <summary>
    /// Drift with a boost charge, stepped once per simulation tick. Holding the brake while steering hard at
    /// speed starts a drift (the car keeps its speed and slides instead of braking); the charge grows with
    /// time and steering into the drift; releasing the brake fires a boost whose length depends on the stage.
    /// Leaving the drift any other way (too slow, straightened out, airborne too long) cancels the charge.
    /// Pure and allocation-free; <c>DriftBoost</c> applies it to the car.
    /// </summary>
    public sealed class DriftCharge
    {
        public float EnterSteer = 0.4f;
        public float EnterSpeed = 11f;      // m/s (~40 km/h)
        public float KeepSpeed = 7f;        // m/s
        public float KeepSteer = 0.1f;
        public float AirGraceSeconds = 0.4f;
        /// <summary>The steer may reach <see cref="EnterSteer"/> this long after the brake press (touch players press first).</summary>
        public float PressWindow = 0.25f;
        /// <summary>Charge (s) needed for stage 1, 2, 3.</summary>
        public float[] StageCharge = { 0.6f, 1.4f, 2.4f };
        /// <summary>Boost seconds on release at stage 1, 2, 3.</summary>
        public float[] StageBoost = { 0.5f, 1.0f, 1.6f };

        public bool Drifting { get; private set; }
        /// <summary>+1 = drifting to the right, −1 = left.</summary>
        public int Direction { get; private set; }
        public float Charge { get; private set; }
        public float DriftSeconds { get; private set; }
        private float airTime, sincePress = float.MaxValue;
        private bool brakeWasHeld;

        public int Stage
        {
            get
            {
                int s = 0;
                for (int i = 0; i < StageCharge.Length; i++) if (Charge >= StageCharge[i]) s = i + 1;
                return s;
            }
        }

        /// <summary>
        /// One tick. Returns the boost seconds to apply now (0 = none). A drift only starts within
        /// <see cref="PressWindow"/> of pressing the brake, so a long hard stop that turns into a corner stays a stop.
        /// </summary>
        public float Step(bool brake, float steer, float speedMs, bool grounded, float dt)
        {
            if (brake && !brakeWasHeld) sincePress = 0f; else if (brake) sincePress += dt; else sincePress = float.MaxValue;
            brakeWasHeld = brake;
            if (!Drifting)
            {
                if (brake && sincePress <= PressWindow && grounded && speedMs >= EnterSpeed && Math.Abs(steer) >= EnterSteer)
                {
                    Drifting = true; Direction = steer > 0f ? 1 : -1; Charge = 0f; DriftSeconds = 0f; airTime = 0f;
                }
                return 0f;
            }

            airTime = grounded ? 0f : airTime + dt;
            if (!brake)
            {
                float boost = Stage > 0 ? StageBoost[Stage - 1] : 0f;
                End();
                return boost;
            }
            if (speedMs < KeepSpeed || airTime > AirGraceSeconds || steer * Direction < -0.5f) { End(); return 0f; }

            DriftSeconds += dt;
            // Steering into the drift charges up to twice as fast as holding it loosely; counter-steer holds.
            float into = steer * Direction;
            if (into >= KeepSteer) Charge += dt * (0.5f + into);
            return 0f;
        }

        public void Cancel() { End(); brakeWasHeld = false; sincePress = float.MaxValue; }

        private void End() { Drifting = false; Charge = 0f; DriftSeconds = 0f; airTime = 0f; Direction = 0; }
    }
}
