using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Core.Economy
{
    /// <summary>
    /// Builds livery stripes as geometry: every body triangle is clipped against axis-aligned boxes (plus a
    /// face-normal filter) and the surviving polygons are pushed out a few millimetres along the face normal.
    /// Needs no UVs, textures or decal passes, so it works on the procedural body and on imported meshes alike.
    /// Runs once per car at spawn; not for per-frame use.
    /// </summary>
    public static class LiveryClipper
    {
        /// <summary>One convex region in mesh space, with the range of face normals it applies to.</summary>
        public struct Region
        {
            public Vector3 Min, Max;
            public float MinNormalY, MaxNormalY;
            public float MinAbsNormalX, MaxAbsNormalX;

            public Region(Vector3 min, Vector3 max, float minNy = -1f, float maxNy = 1f, float minAbsNx = 0f, float maxAbsNx = 1f)
            {
                Min = min; Max = max; MinNormalY = minNy; MaxNormalY = maxNy; MinAbsNormalX = minAbsNx; MaxAbsNormalX = maxAbsNx;
            }

            public bool Accepts(Vector3 n) => n.y >= MinNormalY && n.y <= MaxNormalY && Mathf.Abs(n.x) >= MinAbsNormalX && Mathf.Abs(n.x) <= MaxAbsNormalX;
        }

        /// <summary>
        /// Regions of a pattern for a body with these mesh-space bounds (x right, y up, z forward).
        /// Sizes are fractions of the body, so any car gets proportionate stripes.
        /// </summary>
        public static List<Region> RegionsFor(LiveryPattern pattern, Bounds body)
        {
            var list = new List<Region>(2);
            Vector3 c = body.center, e = body.extents;
            float yLo = body.min.y, yHi = body.max.y, zLo = body.min.z, zHi = body.max.z;
            float X(float u) => c.x + u * e.x;
            float Y(float v) => yLo + v * (yHi - yLo);
            float Z(float w) => zLo + w * (zHi - zLo);
            const float Pad = 0.01f;
            switch (pattern)
            {
                case LiveryPattern.CenterStripe:
                    // Over the top and down the nose and tail; not on the flanks or the floor.
                    list.Add(new Region(new Vector3(X(-0.13f), Y(0.2f), zLo - Pad), new Vector3(X(0.13f), yHi + Pad, zHi + Pad), -0.2f, 1f, 0f, 0.8f));
                    break;
                case LiveryPattern.TwinStripes:
                    list.Add(new Region(new Vector3(X(-0.30f), Y(0.2f), zLo - Pad), new Vector3(X(-0.17f), yHi + Pad, zHi + Pad), -0.2f, 1f, 0f, 0.8f));
                    list.Add(new Region(new Vector3(X(0.17f), Y(0.2f), zLo - Pad), new Vector3(X(0.30f), yHi + Pad, zHi + Pad), -0.2f, 1f, 0f, 0.8f));
                    break;
                case LiveryPattern.SideBand:
                    list.Add(new Region(new Vector3(body.min.x - Pad, Y(0.30f), Z(0.08f)), new Vector3(body.max.x + Pad, Y(0.44f), Z(0.92f)), -0.6f, 0.6f, 0.45f, 1f));
                    break;
                case LiveryPattern.Hood:
                    list.Add(new Region(new Vector3(X(-0.62f), Y(0.25f), Z(0.60f)), new Vector3(X(0.62f), yHi + Pad, zHi + Pad), 0.25f, 1f, 0f, 0.9f));
                    break;
                case LiveryPattern.Tail:
                    list.Add(new Region(new Vector3(body.min.x - Pad, Y(0.25f), zLo - Pad), new Vector3(body.max.x + Pad, yHi + Pad, Z(0.22f)), -0.3f, 1f, 0f, 1f));
                    break;
            }
            return list;
        }

        /// <summary>
        /// Clip triangles (<paramref name="tris"/> indexes <paramref name="verts"/>) against the regions and append
        /// flat-shaded output. Returns the number of output triangles.
        /// </summary>
        public static int Build(IList<Vector3> verts, IList<int> tris, IList<Region> regions, float offset,
                                List<Vector3> outVerts, List<Vector3> outNormals, List<int> outTris)
        {
            var poly = new List<Vector3>(12);
            var tmp = new List<Vector3>(12);
            int count = 0;
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                float len = n.magnitude;
                if (len < 1e-10f) continue;
                n /= len;
                for (int r = 0; r < regions.Count; r++)
                {
                    var region = regions[r];
                    if (!region.Accepts(n)) continue;
                    poly.Clear(); poly.Add(a); poly.Add(b); poly.Add(c);
                    for (int axis = 0; axis < 3 && poly.Count >= 3; axis++)
                    {
                        ClipAxis(poly, tmp, axis, region.Min[axis], true);
                        ClipAxis(tmp, poly, axis, region.Max[axis], false);
                    }
                    if (poly.Count < 3) continue;
                    int start = outVerts.Count;
                    Vector3 push = n * offset;
                    for (int i = 0; i < poly.Count; i++) { outVerts.Add(poly[i] + push); outNormals.Add(n); }
                    for (int i = 1; i + 1 < poly.Count; i++) { outTris.Add(start); outTris.Add(start + i); outTris.Add(start + i + 1); count++; }
                }
            }
            return count;
        }

        // Sutherland–Hodgman against one axis-aligned plane: keep p[axis] >= limit (lower) or <= limit (upper).
        private static void ClipAxis(List<Vector3> input, List<Vector3> output, int axis, float limit, bool lower)
        {
            output.Clear();
            int n = input.Count;
            if (n == 0) return;
            for (int i = 0; i < n; i++)
            {
                Vector3 cur = input[i], prev = input[(i + n - 1) % n];
                float dc = lower ? cur[axis] - limit : limit - cur[axis];
                float dp = lower ? prev[axis] - limit : limit - prev[axis];
                bool curIn = dc >= 0f, prevIn = dp >= 0f;
                if (curIn != prevIn) output.Add(Vector3.Lerp(prev, cur, dp / (dp - dc)));
                if (curIn) output.Add(cur);
            }
        }
    }
}
