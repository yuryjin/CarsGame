using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame.World.Gen
{
    /// <summary>
    /// Marching squares over a solidity field: caps the solid part of each grid cell and drops a
    /// cliff wall along the zero line down to the abyss floor. Shared by the terrain and the track
    /// causeway so their cliffs look the same.
    /// </summary>
    public static class CliffMeshGen
    {
        private static readonly Color32 Topsoil = MeshChunkWriter.Srgb(0.42f, 0.31f, 0.20f);
        private static readonly Color32 Rock = MeshChunkWriter.Srgb(0.47f, 0.44f, 0.41f);
        private static readonly Color32 RockDark = MeshChunkWriter.Srgb(0.24f, 0.22f, 0.23f);
        public static readonly Color32 Abyss = MeshChunkWriter.Srgb(0.035f, 0.035f, 0.05f);

        private static readonly Vector3[] poly = new Vector3[8];
        private static readonly bool[] onEdge = new bool[8];
        private static readonly Vector3[] corner = new Vector3[4];
        private static readonly float[] value = new float[4];

        /// <summary>
        /// One grid cell. Corners in plan order (i,j), (i+1,j), (i+1,j+1), (i,j+1); a value &gt;= 0 is solid.
        /// A fully solid cell is left to the caller (it has its own triangulation); a mixed cell gets
        /// its cap through <paramref name="addCap"/> (already facing up) and its walls in <paramref name="walls"/>.
        /// Returns false for a fully solid cell.
        /// </summary>
        public static bool Cell(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float f0, float f1, float f2, float f3,
            Action<Vector3, Vector3, Vector3> addCap, MeshChunkWriter walls, float floorY)
        {
            corner[0] = p0; corner[1] = p1; corner[2] = p2; corner[3] = p3;
            value[0] = f0; value[1] = f1; value[2] = f2; value[3] = f3;
            int mask = (f0 >= 0f ? 1 : 0) | (f1 >= 0f ? 2 : 0) | (f2 >= 0f ? 4 : 0) | (f3 >= 0f ? 8 : 0);
            if (mask == 15) return false;
            if (mask == 0) return true;

            bool saddle = mask == 5 || mask == 10;
            if (saddle && (f0 + f1 + f2 + f3) < 0f)
            {
                // Two separate solid corners: one triangle each.
                for (int k = 0; k < 4; k++)
                {
                    if (value[k] < 0f) continue;
                    int prev = (k + 3) & 3, next = (k + 1) & 3;
                    poly[0] = Edge(prev, k); onEdge[0] = true;
                    poly[1] = corner[k]; onEdge[1] = false;
                    poly[2] = Edge(k, next); onEdge[2] = true;
                    Emit(3, addCap, walls, floorY);
                }
                return true;
            }

            // Walk the cell counter-clockwise (in plan): the solid polygon, with crossings on the edges.
            int n = 0;
            for (int k = 0; k < 4; k++)
            {
                int next = (k + 1) & 3;
                if (value[k] >= 0f) { poly[n] = corner[k]; onEdge[n++] = false; }
                if ((value[k] >= 0f) != (value[next] >= 0f)) { poly[n] = Edge(k, next); onEdge[n++] = true; }
            }
            Emit(n, addCap, walls, floorY);
            return true;
        }

        // Crossing point on a cell edge, interpolated from the canonical end so neighbours get bit-identical points.
        private static Vector3 Edge(int a, int b)
        {
            Vector3 pa = corner[a], pb = corner[b];
            float fa = value[a], fb = value[b];
            if (pa.x > pb.x || (pa.x == pb.x && pa.z > pb.z)) { (pa, pb) = (pb, pa); (fa, fb) = (fb, fa); }
            float t = Mathf.Clamp01(fa / (fa - fb));
            return Vector3.LerpUnclamped(pa, pb, t);
        }

        private static void Emit(int n, Action<Vector3, Vector3, Vector3> addCap, MeshChunkWriter walls, float floorY)
        {
            for (int i = 1; i + 1 < n; i++) Up(addCap, poly[0], poly[i], poly[i + 1]);
            // The polygon is counter-clockwise in plan (solid on the left): a wall between two crossings faces the void on the right.
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (onEdge[i] && onEdge[j]) Wall(walls, poly[i], poly[j], floorY);
            }
        }

        public static void Up(Action<Vector3, Vector3, Vector3> addCap, Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f) addCap(a, c, b); else addCap(a, b, c);
        }

        /// <summary>
        /// Cliff from the top edge a→b (void on the right when looking from above) down to <paramref name="floorY"/>:
        /// a band of topsoil, then rock darkening into the abyss. Inner rows wobble by a position hash, so
        /// neighbouring segments share their vertices.
        /// </summary>
        public static void Wall(MeshChunkWriter w, Vector3 a, Vector3 b, float floorY)
        {
            if ((a - b).sqrMagnitude < 1e-6f) return;
            float shade = 0.9f + 0.16f * Hash(a.x + b.x, a.z + b.z, 3);
            Vector3 A1 = Row(a, a.y - 2.2f, 1), B1 = Row(b, b.y - 2.2f, 1);
            Vector3 A2 = Row(a, Mathf.Lerp(a.y, floorY, 0.3f), 2), B2 = Row(b, Mathf.Lerp(b.y, floorY, 0.3f), 2);
            Vector3 A3 = new Vector3(a.x, floorY, a.z), B3 = new Vector3(b.x, floorY, b.z);
            Color32 soil = MeshChunkWriter.Scale(Topsoil, shade), rock = MeshChunkWriter.Scale(Rock, shade), dark = MeshChunkWriter.Scale(RockDark, shade);
            Band(w, a, b, B1, A1, soil, soil);
            Band(w, A1, B1, B2, A2, rock, dark);
            Band(w, A2, B2, B3, A3, dark, Abyss);
        }

        private static void Band(MeshChunkWriter w, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 top, Color32 bottom)
        {
            w.AddTriangle(a, b, c, top, top, bottom);
            w.AddTriangle(a, c, d, top, bottom, bottom);
        }

        private static Vector3 Row(Vector3 top, float y, int salt)
        {
            const float wobble = 0.7f;
            return new Vector3(top.x + (Hash(top.x, top.z, salt) - 0.5f) * 2f * wobble, y, top.z + (Hash(top.x, top.z, salt + 7) - 0.5f) * 2f * wobble);
        }

        private static float Hash(float x, float z, int salt)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(x * 16f) * 73856093u ^ (uint)Mathf.RoundToInt(z * 16f) * 19349663u ^ (uint)salt * 83492791u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }

        /// <summary>
        /// The strip the track runs on: flat ground within the shoulder, cut by the canyon under the bridge,
        /// with cliffs on every edge. Survives the collapse, so it is its own mesh (and collider).
        /// </summary>
        public static List<Mesh> BuildCauseway(ChasmLayout layout, TerrainSettings terrain, float cell = 2f)
        {
            var s = layout.Spline;
            float margin = layout.Settings.shoulder + cell * 2f;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < s.PointCount; i++)
            {
                var p = s.Point(i);
                min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z));
            }
            min -= Vector2.one * margin; max += Vector2.one * margin;
            int nx = Mathf.CeilToInt((max.x - min.x) / cell) + 1, nz = Mathf.CeilToInt((max.y - min.y) / cell) + 1;
            var f = new float[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = min.x + i * cell, z = min.y + j * cell;
                    f[i, j] = layout.Causeway(x, z, layout.DistanceToTrack(x, z));
                }

            var noise = new PropNoise(terrain.seed);
            var top = new MeshChunkWriter("Causeway");
            var walls = new MeshChunkWriter("CausewayCliffs");
            float y = terrain.baseY;
            Action<Vector3, Vector3, Vector3> cap = (a, b, c) => top.AddTriangle(a, b, c, TerrainMeshGen.FaceColour(a, b, c, noise, terrain));
            for (int i = 0; i < nx - 1; i++)
                for (int j = 0; j < nz - 1; j++)
                {
                    float x0 = min.x + i * cell, z0 = min.y + j * cell;
                    Vector3 p0 = new Vector3(x0, y, z0), p1 = new Vector3(x0 + cell, y, z0), p2 = new Vector3(x0 + cell, y, z0 + cell), p3 = new Vector3(x0, y, z0 + cell);
                    if (!Cell(p0, p1, p2, p3, f[i, j], f[i + 1, j], f[i + 1, j + 1], f[i, j + 1], cap, walls, -layout.Settings.depth))
                    {
                        Up(cap, p0, p1, p2); Up(cap, p0, p2, p3);
                    }
                }
            var result = new List<Mesh>(top.Finish());
            result.AddRange(walls.Finish());
            return result;
        }

        /// <summary>Steel-arch bridge over the canyon: concrete deck flush with the ground, girders, two arches and posts.</summary>
        public static List<Mesh> BuildBridge(ChasmLayout layout, float groundY)
        {
            var st = layout.Settings;
            var w = new MeshChunkWriter("Bridge");
            var m = layout.BridgeMatrix;          // local: x right, z along the track
            var concrete = MeshChunkWriter.Srgb(0.70f, 0.69f, 0.66f);
            var steel = MeshChunkWriter.Srgb(0.78f, 0.22f, 0.16f);
            var steelDark = MeshChunkWriter.Srgb(0.45f, 0.14f, 0.12f);
            float span = st.bridgeHalfSpan, half = st.bridgeHalfWidth;
            const float deck = 1.1f;

            LowPoly.Box(w, m, new Vector3(0f, groundY - deck * 0.5f, 0f), new Vector3(half * 2f, deck, span * 2f), concrete);
            foreach (float side in new[] { -1f, 1f })
            {
                // edge girders under the deck
                LowPoly.Box(w, m, new Vector3(side * (half - 0.35f), groundY - deck - 0.6f, 0f), new Vector3(0.7f, 1.2f, span * 2f), steel);
                // arch from the canyon walls up under the deck, posts every few metres
                const int segs = 10;
                float spring = groundY - 16f, crown = groundY - deck - 1.3f;
                Vector3 Arch(float t)
                {
                    float z = Mathf.Lerp(-span - 1f, span + 1f, t);
                    float k = 1f - Mathf.Pow(2f * t - 1f, 2f);
                    return new Vector3(side * (half - 1.2f), Mathf.Lerp(spring, crown, Mathf.Sqrt(k)), z);
                }
                for (int i = 0; i < segs; i++)
                {
                    Vector3 a = Arch(i / (float)segs), b = Arch((i + 1) / (float)segs);
                    Vector3 mid = (a + b) * 0.5f, dir = b - a;
                    var seg = m * Matrix4x4.TRS(mid, Quaternion.LookRotation(dir.normalized, Vector3.up), Vector3.one);
                    LowPoly.Box(w, seg, Vector3.zero, new Vector3(0.9f, 1.0f, dir.magnitude + 0.3f), steel);
                    if (i > 0)
                    {
                        float top = groundY - deck - 1.2f;
                        if (top - a.y > 0.8f)
                            LowPoly.Box(w, m, new Vector3(a.x, (a.y + top) * 0.5f, a.z), new Vector3(0.35f, top - a.y, 0.35f), steelDark);
                    }
                }
            }
            // cross beams between the arches
            for (int i = -2; i <= 2; i++)
                LowPoly.Box(w, m, new Vector3(0f, groundY - deck - 0.5f, i * span * 0.4f), new Vector3(half * 2f - 1.4f, 0.5f, 0.5f), steelDark);
            return w.Finish();
        }

        /// <summary>Dark floor far below, so the void reads as depth instead of sky.</summary>
        public static Mesh BuildFloor(Vector2 centre, float halfSize, float y)
        {
            var w = new MeshChunkWriter("AbyssFloor");
            Vector3 a = new Vector3(centre.x - halfSize, y, centre.y - halfSize), b = new Vector3(centre.x + halfSize, y, centre.y - halfSize);
            Vector3 c = new Vector3(centre.x + halfSize, y, centre.y + halfSize), d = new Vector3(centre.x - halfSize, y, centre.y + halfSize);
            Up((p, q, r) => w.AddTriangle(p, q, r, Abyss), a, b, c);
            Up((p, q, r) => w.AddTriangle(p, q, r, Abyss), a, c, d);
            return w.Finish()[0];
        }
    }
}
