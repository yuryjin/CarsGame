using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame.World.Gen
{
    [System.Serializable]
    public sealed class TerrainSettings
    {
        public float half = 520f;          // terrain extends ±half metres around the centre
        public float innerHalf = 300f;     // fine grid inside, coarse outside
        public float innerCell = 6f;
        public float outerCell = 20f;
        public float flatRadius = 50f;     // metres from the centre line that stay perfectly flat
        public float blendRadius = 95f;    // hills reach full amplitude here
        public float baseY = -0.05f;       // matches the existing ground plane collider
        public float hillAmplitude = 38f;
        public float infieldAmplitude = 5f;
        public float hillFrequency = 1f / 110f;
        public int seed = 1234;
    }

    /// <summary>
    /// Low-poly terrain: flat corridor around the track spline, noise hills further out, coarse outer
    /// ring. Pure mesh generation (no colliders) — the physics ground stays the original flat plane.
    /// </summary>
    public static class TerrainMeshGen
    {
        private static readonly Color32 Grass = MeshChunkWriter.Srgb(0.31f, 0.56f, 0.22f);
        private static readonly Color32 GrassLight = MeshChunkWriter.Srgb(0.47f, 0.66f, 0.27f);
        private static readonly Color32 Dry = MeshChunkWriter.Srgb(0.68f, 0.63f, 0.33f);
        private static readonly Color32 Dirt = MeshChunkWriter.Srgb(0.48f, 0.37f, 0.25f);
        private static readonly Color32 Rock = MeshChunkWriter.Srgb(0.52f, 0.50f, 0.48f);

        public static float[] Axis(TerrainSettings s)
        {
            var list = new List<float>();
            for (float x = -s.half; x < -s.innerHalf - 0.01f; x += s.outerCell) list.Add(x);
            for (float x = -s.innerHalf; x < s.innerHalf - 0.01f; x += s.innerCell) list.Add(x);
            for (float x = s.innerHalf; x <= s.half + 0.01f; x += s.outerCell) list.Add(x);
            return list.ToArray();
        }

        public static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-5f, b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Ray-casting point-in-polygon on the loop points (XZ).</summary>
        public static bool InsideLoop(TrackSpline spline, float x, float z)
        {
            bool inside = false;
            int n = spline.PointCount;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector3 pi = spline.Point(i), pj = spline.Point(j);
                if ((pi.z > z) != (pj.z > z) && x < (pj.x - pi.x) * (z - pi.z) / (pj.z - pi.z) + pi.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Height of the visual terrain at (x,z). distToTrack in metres to the centre line.</summary>
        public static float Height(float x, float z, float distToTrack, bool inside, TerrainSettings s, PropNoise noise, out float hillness)
        {
            float mask = SmoothStep(s.flatRadius, s.blendRadius, distToTrack);
            float n = noise.Fbm(x, z, s.hillFrequency, 4, 2.1f, 0.5f) * 0.5f + 0.5f;
            hillness = Mathf.Pow(Mathf.Clamp01(n), 1.5f);
            float amp = inside ? s.infieldAmplitude : Mathf.Lerp(s.hillAmplitude * 0.45f, s.hillAmplitude, Mathf.Clamp01((distToTrack - s.blendRadius) / 300f));
            float h = s.baseY + mask * amp * hillness;
            if (!inside)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                h += SmoothStep(s.half - 120f, s.half, r) * 36f;    // rim rises so the edge never reads as a cliff
            }
            return h;
        }

        public static float DistanceToTrack(TrackSpline spline, Vector3 p)
        {
            int hint = -1;
            float d = spline.Project(p, ref hint);
            Vector3 c = spline.Position(d); c.y = 0f; p.y = 0f;
            return Vector3.Distance(c, p);
        }

        /// <summary>Builds the terrain around <paramref name="centre"/> as four quadrant chunks (each may split further).</summary>
        public static List<Mesh> Build(TrackSpline spline, TerrainSettings s, Vector2 centre)
        {
            var noise = new PropNoise(s.seed);
            var ax = Axis(s);
            int n = ax.Length;
            var pos = new Vector3[n, n];
            var hill = new float[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float wx = centre.x + ax[i], wz = centre.y + ax[j];
                    float dist = DistanceToTrack(spline, new Vector3(wx, 0f, wz));
                    bool inside = InsideLoop(spline, wx, wz);
                    float h = Height(ax[i], ax[j], dist, inside, s, noise, out float hl);
                    pos[i, j] = new Vector3(wx, h, wz);
                    hill[i, j] = hl;
                }

            var writers = new MeshChunkWriter[4];
            for (int q = 0; q < 4; q++) writers[q] = new MeshChunkWriter("Terrain_Q" + q);
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < n - 1; j++)
                {
                    Vector3 a = pos[i, j], b = pos[i + 1, j], c = pos[i + 1, j + 1], d = pos[i, j + 1];
                    int q = (ax[i] >= 0f ? 1 : 0) + (ax[j] >= 0f ? 2 : 0);
                    var w = writers[q];
                    bool diag = ((i + j) & 1) == 0;
                    if (diag) { AddFace(w, a, c, b, noise, s); AddFace(w, a, d, c, noise, s); }
                    else { AddFace(w, a, d, b, noise, s); AddFace(w, b, d, c, noise, s); }
                }
            var result = new List<Mesh>();
            foreach (var w in writers) result.AddRange(w.Finish());
            return result;
        }

        private static void AddFace(MeshChunkWriter w, Vector3 a, Vector3 b, Vector3 c, PropNoise noise, TerrainSettings s)
        {
            Vector3 centroid = (a + b + c) / 3f;
            Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
            float height = centroid.y - s.baseY;
            float v = noise.Value(centroid.x * 0.045f, centroid.z * 0.045f, 7);
            Color32 col = MeshChunkWriter.Lerp(Grass, GrassLight, v);
            float dry = Mathf.Clamp01((height - 5f) / 14f) * noise.Value(centroid.x * 0.02f, centroid.z * 0.02f, 9);
            col = MeshChunkWriter.Lerp(col, Dry, dry);
            float slope = 1f - Mathf.Clamp01(nrm.y);
            if (slope > 0.30f) col = MeshChunkWriter.Lerp(col, Dirt, Mathf.Clamp01((slope - 0.30f) / 0.15f));
            if (slope > 0.55f) col = MeshChunkWriter.Lerp(col, Rock, Mathf.Clamp01((slope - 0.55f) / 0.15f));
            // Face-constant colour (split vertices) with a tiny per-face jitter for the low-poly look.
            float jitter = 0.94f + 0.12f * noise.Hash(Mathf.RoundToInt(centroid.x * 3f), Mathf.RoundToInt(centroid.z * 3f), 5);
            w.AddTriangle(a, b, c, MeshChunkWriter.Scale(col, jitter));
        }
    }
}
