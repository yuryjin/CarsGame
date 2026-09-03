using UnityEngine;

namespace RacingGame.World
{
    /// <summary>
    /// CPU twin of Sky.shader's gradient + sun + moon (stars omitted): evaluates the sky colour for a
    /// direction so the reflection cubemap of a preset matches what the skybox shows. All colours linear.
    /// </summary>
    public struct SkyParams
    {
        public Color Zenith, Horizon, Ground;
        public float HorizonExp;
        public Vector3 SunDir;
        public Color SunColor;
        public float SunSize1mCos, SunGlow, SunGlowPower;
        public Vector3 MoonDir;
        public Color MoonColor;
        public float MoonSize1mCos;
    }

    public static class SkyMath
    {
        /// <summary>1 - cos(angle) for a disc of the given angular radius in degrees (what the shader compares against).</summary>
        public static float DiscSize(float angularRadiusDeg) => 1f - Mathf.Cos(angularRadiusDeg * Mathf.Deg2Rad);

        private static float Disc(float cosAngle, float size1mCos)
        {
            float d = 1f - cosAngle;
            return 1f - SmoothStep(size1mCos * 0.7f, size1mCos, d);
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-6f, b - a));
            return t * t * (3f - 2f * t);
        }

        public static Color Evaluate(Vector3 dir, in SkyParams p)
        {
            dir = dir.normalized;
            float up = dir.y;
            Color col = up >= 0f
                ? Color.Lerp(p.Horizon, p.Zenith, Mathf.Pow(Mathf.Clamp01(up), p.HorizonExp))
                : Color.Lerp(p.Horizon, p.Ground, Mathf.Clamp01(-up * 3f));

            float c = Vector3.Dot(dir, p.SunDir.normalized);
            float sunVis = Mathf.Clamp01((up + 0.08f) * 12f);
            float sun = Disc(c, p.SunSize1mCos) + Mathf.Pow(Mathf.Clamp01(c), p.SunGlowPower) * p.SunGlow;
            col += p.SunColor * (sun * sunVis);

            if (p.MoonColor.r + p.MoonColor.g + p.MoonColor.b > 0f)
            {
                float mc = Vector3.Dot(dir, p.MoonDir.normalized);
                float moon = Disc(mc, p.MoonSize1mCos) + Mathf.Pow(Mathf.Clamp01(mc), 64f) * 0.25f;
                col += p.MoonColor * (moon * Mathf.Clamp01((up + 0.05f) * 12f));
            }
            col.a = 1f;
            return col;
        }

        /// <summary>Direction of a cubemap texel (Unity face order +X -X +Y -Y +Z -Z, u/v in [0,1]).</summary>
        public static Vector3 CubeDirection(CubemapFace face, float u, float v)
        {
            float a = 2f * u - 1f, b = 2f * v - 1f;
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -b, -a);
                case CubemapFace.NegativeX: return new Vector3(-1f, -b, a);
                case CubemapFace.PositiveY: return new Vector3(a, 1f, b);
                case CubemapFace.NegativeY: return new Vector3(a, -1f, -b);
                case CubemapFace.PositiveZ: return new Vector3(a, -b, 1f);
                default: return new Vector3(-a, -b, -1f);
            }
        }

        /// <summary>Fill a cubemap with the sky (stored gamma-encoded for an sRGB texture, HDR clipped).</summary>
        public static void FillCubemap(Cubemap cube, in SkyParams p, Color[] scratch)
        {
            int size = cube.width;
            if (scratch == null || scratch.Length != size * size) scratch = new Color[size * size];
            for (int f = 0; f < 6; f++)
            {
                var face = (CubemapFace)f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Vector3 dir = CubeDirection(face, (x + 0.5f) / size, (y + 0.5f) / size);
                        Color c = Evaluate(dir, p);
                        c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 1f;
                        scratch[y * size + x] = c.gamma;
                    }
                cube.SetPixels(scratch, face);
            }
            cube.Apply(true, false);
        }
    }
}
