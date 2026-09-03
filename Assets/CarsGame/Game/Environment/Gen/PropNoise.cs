using UnityEngine;

namespace RacingGame.World.Gen
{
    /// <summary>Deterministic 2D value noise + fBm (no UnityEngine.Random: the race RNG streams stay untouched).</summary>
    public sealed class PropNoise
    {
        private readonly int seed;
        public PropNoise(int seed) { this.seed = seed; }

        public float Hash(int x, int y, int salt = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)(seed * 1274126177) ^ (uint)(salt * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Smooth value noise in [0,1].</summary>
        public float Value(float x, float y, int salt = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi, salt), b = Hash(xi + 1, yi, salt), c = Hash(xi, yi + 1, salt), d = Hash(xi + 1, yi + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Fractal sum in [-1,1] (approximately), frequency in 1/metres.</summary>
        public float Fbm(float x, float y, float frequency, int octaves, float lacunarity = 2f, float gain = 0.5f, int salt = 0)
        {
            float sum = 0f, amp = 1f, norm = 0f, f = frequency;
            for (int i = 0; i < octaves; i++)
            {
                sum += (Value(x * f, y * f, salt + i) * 2f - 1f) * amp;
                norm += amp; amp *= gain; f *= lacunarity;
            }
            return sum / norm;
        }
    }
}
