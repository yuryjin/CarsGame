using UnityEngine;

namespace RacingGame.World.Gen
{
    /// <summary>
    /// Ring of faceted mountains far outside the terrain, built from a noise ridge profile around the
    /// circle (varied massifs and peaks instead of a uniform sawtooth). Rendered by StylizedLit in mountain
    /// mode: rgb = tint (× preset mountain colour), alpha = resistance to the horizon colour (0 at the base
    /// so the ring melts into the fog, up to peakAlpha at the summits).
    /// </summary>
    public static class MountainRingGen
    {
        public static Mesh Build(string name, Vector2 centre, float radius, int samples, float minHeight, float maxHeight, float radialJitter, int seed, float peakAlpha, float roughness = 1f)
        {
            var noise = new PropNoise(seed);
            var w = new MeshChunkWriter(name);
            float step = Mathf.PI * 2f / samples;
            Vector3 C(float ang, float r, float y) => new Vector3(centre.x + Mathf.Cos(ang) * r, y, centre.y + Mathf.Sin(ang) * r);

            // Ridge profile: low-frequency massifs + higher-frequency peaks, sampled on the circle (periodic via cos/sin coords).
            float H(int i)
            {
                float a = i * step;
                float cx = Mathf.Cos(a) * 3f, cz = Mathf.Sin(a) * 3f;
                float massif = noise.Fbm(cx, cz, 1f, 2, 2f, 0.5f, 1) * 0.5f + 0.5f;          // 0..1, ~3 bumps around
                float peaks = noise.Fbm(cx * 4f, cz * 4f, 1f, 3, 2.2f, 0.55f, 2) * 0.5f + 0.5f;  // finer peaks
                float t = Mathf.Clamp01(massif * 0.55f + peaks * 0.45f * roughness);
                return Mathf.Lerp(minHeight, maxHeight, Mathf.Pow(t, 1.35f));
            }
            float R(int i)
            {
                float a = i * step;
                return radius + noise.Fbm(Mathf.Cos(a) * 2f, Mathf.Sin(a) * 2f, 1f, 2, 2f, 0.5f, 3) * radialJitter;
            }

            for (int i = 0; i < samples; i++)
            {
                int i1 = (i + 1) % samples;
                float a0 = i * step, a1 = i1 * step;
                float h0 = H(i), h1 = H(i1);
                float r0 = R(i), r1 = R(i1);
                Vector3 b0 = C(a0, r0 - 25f, -4f), b1 = C(a1, r1 - 25f, -4f);   // base pulled towards the viewer: slopes face inward
                Vector3 p0 = C(a0, r0, h0), p1 = C(a1, r1, h1);
                float alpha0 = Mathf.Lerp(0.35f, 1f, (h0 - minHeight) / Mathf.Max(1f, maxHeight - minHeight)) * peakAlpha;
                float alpha1 = Mathf.Lerp(0.35f, 1f, (h1 - minHeight) / Mathf.Max(1f, maxHeight - minHeight)) * peakAlpha;
                byte shade = (byte)(215 + 40 * noise.Hash(i, 4));
                var cBase = new Color32(255, 255, 255, 0);
                var cp0 = new Color32(shade, shade, shade, (byte)(alpha0 * 255f));
                var cp1 = new Color32(shade, shade, shade, (byte)(alpha1 * 255f));
                // Two facets per sample; alternate the diagonal so facets vary.
                if ((i & 1) == 0) { AddInward(w, b0, p0, b1, centre, cBase, cp0, cBase); AddInward(w, b1, p0, p1, centre, cBase, cp0, cp1); }
                else { AddInward(w, b0, p0, p1, centre, cBase, cp0, cp1); AddInward(w, b0, p1, b1, centre, cBase, cp1, cBase); }
            }
            return w.Finish()[0];
        }

        // Winding so the face points towards the ring centre (the viewer).
        private static void AddInward(MeshChunkWriter w, Vector3 a, Vector3 b, Vector3 c, Vector2 centre, Color32 ca, Color32 cb, Color32 cc)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            Vector3 toCentre = new Vector3(centre.x, 0f, centre.y) - (a + b + c) / 3f;
            if (Vector3.Dot(n, toCentre) < 0f) w.AddTriangle(a, c, b, ca, cc, cb); else w.AddTriangle(a, b, c, ca, cb, cc);
        }
    }
}
