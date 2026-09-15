using System;
using UnityEngine;

namespace RacingGame.Core.Track
{
    /// <summary>
    /// Per-metre driving hints sampled along a <see cref="TrackSpline"/>: signed local curvature,
    /// a braking-aware target speed and a smoothed racing-line offset. Built once per track
    /// direction (allocates then), read every tick without allocation.
    /// </summary>
    public sealed class TrackProfile
    {
        [Serializable]
        public struct Settings
        {
            [Tooltip("Sample spacing along the loop (m).")] public float step;
            [Tooltip("Window over which local curvature is measured (m). Short = sees chicanes.")] public float curvatureWindow;
            [Tooltip("Usable lateral acceleration (m/s²): corner speed = sqrt(grip / curvature).")] public float lateralGrip;
            [Tooltip("Planned braking deceleration (m/s²) for the backward pass.")] public float brakeDecel;
            [Tooltip("Upper speed bound for the profile (m/s).")] public float topSpeed;
            [Tooltip("Racing-line offset per degree/metre of curvature (m), towards the inside.")] public float laneGain;
            [Tooltip("Largest racing-line offset from the centre (m).")] public float laneLimit;
            [Tooltip("Moving-average window for the racing line (m).")] public float laneSmoothWindow;

            public static Settings Default => new Settings
            {
                step = 2f, curvatureWindow = 12f, lateralGrip = 13f, brakeDecel = 6f, topSpeed = 40f,
                laneGain = 1.2f, laneLimit = 2.8f, laneSmoothWindow = 20f
            };
        }

        private readonly float[] curvature;   // deg/m, + = right-hand turn
        private readonly float[] speed;       // m/s
        private readonly float[] lane;        // m, + = right
        private readonly float step;

        public float Length { get; }
        public int SampleCount => speed.Length;

        public TrackProfile(TrackSpline spline, Settings s)
        {
            if (spline == null) throw new ArgumentNullException(nameof(spline));
            Length = spline.Length;
            int n = Mathf.Max(8, Mathf.RoundToInt(Length / Mathf.Max(0.5f, s.step)));
            step = Length / n;
            curvature = new float[n];
            speed = new float[n];
            lane = new float[n];
            float half = Mathf.Max(step, s.curvatureWindow) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                float d = i * step;
                curvature[i] = SignedTurn(spline.Tangent(d - half), spline.Tangent(d + half)) / (2f * half);
            }

            // Corner speed from the local curvature, then a backward braking pass around the loop
            // (twice, so the wrap at the start line is covered).
            float top = Mathf.Max(1f, s.topSpeed);
            for (int i = 0; i < n; i++)
            {
                float kappa = Mathf.Abs(curvature[i]) * Mathf.Deg2Rad;
                speed[i] = kappa > 1e-5f ? Mathf.Min(top, Mathf.Sqrt(Mathf.Max(0.1f, s.lateralGrip) / kappa)) : top;
            }
            float brake = 2f * Mathf.Max(0.1f, s.brakeDecel) * step;
            for (int pass = 0; pass < 2; pass++)
                for (int i = n - 1; i >= 0; i--)
                {
                    float next = speed[(i + 1) % n];
                    speed[i] = Mathf.Min(speed[i], Mathf.Sqrt(next * next + brake));
                }

            // Racing line: towards the inside of each bend, smoothed so opposite bends of a chicane
            // cancel into a straighter line.
            var raw = new float[n];
            for (int i = 0; i < n; i++) raw[i] = Mathf.Clamp(curvature[i] * s.laneGain, -s.laneLimit, s.laneLimit);
            int w = Mathf.Max(0, Mathf.RoundToInt(s.laneSmoothWindow / step * 0.5f));
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int k = -w; k <= w; k++) sum += raw[((i + k) % n + n) % n];
                lane[i] = sum / (2 * w + 1);
            }
        }

        /// <summary>Signed heading change from a to b in degrees, + = clockwise seen from above (a right turn).</summary>
        public static float SignedTurn(Vector3 a, Vector3 b)
        {
            float cross = a.z * b.x - a.x * b.z;
            float dot = a.x * b.x + a.z * b.z;
            return Mathf.Atan2(cross, dot) * Mathf.Rad2Deg;
        }

        public float SpeedAt(float d) => Sample(speed, d);
        public float LaneAt(float d) => Sample(lane, d);
        public float CurvatureAt(float d) => Sample(curvature, d);

        /// <summary>Lowest target speed within [d, d + window] (m/s).</summary>
        public float MinSpeedAhead(float d, float window)
        {
            float best = SpeedAt(d);
            for (float s = step; s <= window; s += step) best = Mathf.Min(best, SpeedAt(d + s));
            return best;
        }

        private float Sample(float[] values, float d)
        {
            int n = values.Length;
            float x = d / step;
            x %= n; if (x < 0f) x += n;
            int i = (int)x;
            float t = x - i;
            return values[i % n] + (values[(i + 1) % n] - values[i % n]) * t;
        }
    }
}
