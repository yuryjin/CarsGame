using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Flat-shaded low-poly mesh assembly for car bodies. Every triangle gets its own vertices and face normal,
    /// so edges stay crisp in the game's stylised look. Faces are wound outward automatically from a point
    /// inside the solid, so the builders only list corners. Geometry is kept per material slot and baked into
    /// one mesh per renderer (<see cref="Build"/>).
    /// </summary>
    public sealed class BodyMesh
    {
        public enum Slot { Paint, Dark, Glass, Head, Tail, Metal, Tire }
        public const int SlotCount = 7;

        private readonly List<Vector3>[] verts = new List<Vector3>[SlotCount];
        private readonly List<Vector3>[] normals = new List<Vector3>[SlotCount];

        public bool Has(Slot s) => verts[(int)s] != null && verts[(int)s].Count > 0;

        /// <summary>One triangle facing away from <paramref name="inside"/>.</summary>
        public void Tri(Slot s, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            // Unity front faces are clockwise seen from outside, which is where cross(b-a, c-a) points.
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) { var t = b; b = c; c = t; n = -n; }
            n.Normalize();
            int k = (int)s;
            verts[k] ??= new List<Vector3>(); normals[k] ??= new List<Vector3>();
            verts[k].Add(a); verts[k].Add(b); verts[k].Add(c);
            normals[k].Add(n); normals[k].Add(n); normals[k].Add(n);
        }

        public void Quad(Slot s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
        {
            Tri(s, a, b, c, inside);
            Tri(s, a, c, d, inside);
        }

        /// <summary>
        /// A six-sided solid from eight corners: bottom left-rear, right-rear, right-front, left-front, then the top
        /// in the same order. Covers boxes, wedges, sloped hulls and frustums.
        /// </summary>
        public void Hull(Slot s, Vector3 b0, Vector3 b1, Vector3 b2, Vector3 b3, Vector3 t0, Vector3 t1, Vector3 t2, Vector3 t3)
        {
            var c = (b0 + b1 + b2 + b3 + t0 + t1 + t2 + t3) * 0.125f;
            Quad(s, b0, b1, b2, b3, c);
            Quad(s, t0, t1, t2, t3, c);
            Quad(s, b0, b1, t1, t0, c);   // rear
            Quad(s, b3, b2, t2, t3, c);   // front
            Quad(s, b0, b3, t3, t0, c);   // left
            Quad(s, b1, b2, t2, t1, c);   // right
        }

        /// <summary>Hull symmetric about x = 0: half-widths and heights at the rear (z0) and front (z1).</summary>
        public void Section(Slot s, float z0, float z1, float yBottom0, float yBottom1, float yTop0, float yTop1,
            float halfBottom0, float halfBottom1, float halfTop0, float halfTop1, float zTop0 = float.NaN, float zTop1 = float.NaN)
        {
            float zt0 = float.IsNaN(zTop0) ? z0 : zTop0, zt1 = float.IsNaN(zTop1) ? z1 : zTop1;
            Hull(s,
                new Vector3(-halfBottom0, yBottom0, z0), new Vector3(halfBottom0, yBottom0, z0), new Vector3(halfBottom1, yBottom1, z1), new Vector3(-halfBottom1, yBottom1, z1),
                new Vector3(-halfTop0, yTop0, zt0), new Vector3(halfTop0, yTop0, zt0), new Vector3(halfTop1, yTop1, zt1), new Vector3(-halfTop1, yTop1, zt1));
        }

        public void Box(Slot s, Vector3 center, Vector3 size) => Box(s, center, size, Quaternion.identity);

        public void Box(Slot s, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            Hull(s, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1));
        }

        /// <summary>Box mirrored to both sides (center.x and -center.x).</summary>
        public void BoxPair(Slot s, Vector3 center, Vector3 size) => BoxPair(s, center, size, Quaternion.identity);

        public void BoxPair(Slot s, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Box(s, center, size, rotation);
            var m = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);   // mirror of the rotation in x
            Box(s, new Vector3(-center.x, center.y, center.z), size, m);
        }

        /// <summary>Closed cylinder from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public void Cylinder(Slot s, Vector3 a, Vector3 b, float radius, int segments = 8)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 1e-8f) return;
            Vector3 dir = axis.normalized;
            Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(dir, u);
            Vector3 mid = (a + b) * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i * Mathf.PI * 2f / segments, t1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 r0 = (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * radius, r1 = (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * radius;
                Quad(s, a + r0, a + r1, b + r1, b + r0, mid);
                Tri(s, a, a + r0, a + r1, mid);
                Tri(s, b, b + r0, b + r1, mid);
            }
        }

        public void Tube(Slot s, Vector3 a, Vector3 b, float radius) => Cylinder(s, a, b, radius, 6);

        /// <summary>Tube mirrored to both sides.</summary>
        public void TubePair(Slot s, Vector3 a, Vector3 b, float radius)
        {
            Tube(s, a, b, radius);
            Tube(s, new Vector3(-a.x, a.y, a.z), new Vector3(-b.x, b.y, b.z), radius);
        }

        /// <summary>Pointed cone (spikes).</summary>
        public void Cone(Slot s, Vector3 baseCenter, Vector3 tip, float radius, int segments = 6)
        {
            Vector3 dir = (tip - baseCenter).normalized;
            Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(dir, u);
            Vector3 inside = Vector3.Lerp(baseCenter, tip, 0.25f);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i * Mathf.PI * 2f / segments, t1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 r0 = baseCenter + (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * radius, r1 = baseCenter + (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * radius;
                Tri(s, r0, r1, tip, inside);
                Tri(s, baseCenter, r0, r1, inside);
            }
        }

        /// <summary>A convex side profile (z, y) extruded across x from <paramref name="x0"/> to <paramref name="x1"/> (tank tracks).</summary>
        public void Extrude(Slot s, Vector2[] profileZY, float x0, float x1)
        {
            Vector2 c2 = Vector2.zero;
            foreach (var p in profileZY) c2 += p;
            c2 /= profileZY.Length;
            var inside = new Vector3((x0 + x1) * 0.5f, c2.y, c2.x);
            for (int i = 0; i < profileZY.Length; i++)
            {
                var p = profileZY[i]; var q = profileZY[(i + 1) % profileZY.Length];
                Quad(s, new Vector3(x0, p.y, p.x), new Vector3(x1, p.y, p.x), new Vector3(x1, q.y, q.x), new Vector3(x0, q.y, q.x), inside);
                Tri(s, new Vector3(x0, c2.y, c2.x), new Vector3(x0, p.y, p.x), new Vector3(x0, q.y, q.x), inside);
                Tri(s, new Vector3(x1, c2.y, c2.x), new Vector3(x1, p.y, p.x), new Vector3(x1, q.y, q.x), inside);
            }
        }

        /// <summary>
        /// A thin plate standing on a face (windows, grilles, lights, add-on armour): the face is a quad a (bottom-left),
        /// b (bottom-right), c (top-right), d (top-left); the plate covers u0..u1 × v0..v1 of it, raised
        /// <paramref name="thickness"/> away from <paramref name="inside"/>.
        /// </summary>
        public void Panel(Slot s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u0, float u1, float v0, float v1, float thickness, Vector3 inside)
        {
            Vector3 P(float u, float v) => Vector3.Lerp(Vector3.Lerp(a, b, u), Vector3.Lerp(d, c, u), v);
            var n = Vector3.Cross(b - a, d - a).normalized;
            if (Vector3.Dot(n, (a + c) * 0.5f - inside) < 0f) n = -n;
            Vector3 lo = n * 0.004f, hi = n * (0.004f + thickness);
            Vector3 p00 = P(u0, v0), p10 = P(u1, v0), p11 = P(u1, v1), p01 = P(u0, v1);
            Hull(s, p00 + lo, p10 + lo, p11 + lo, p01 + lo, p00 + hi, p10 + hi, p11 + hi, p01 + hi);
        }

        /// <summary>Panel on a face given in the left-hand (x &lt; 0) orientation, repeated on its mirror image.</summary>
        public void PanelPair(Slot s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u0, float u1, float v0, float v1, float thickness, Vector3 inside)
        {
            Panel(s, a, b, c, d, u0, u1, v0, v1, thickness, inside);
            Vector3 M(Vector3 p) => new Vector3(-p.x, p.y, p.z);
            Panel(s, M(a), M(b), M(c), M(d), u0, u1, v0, v1, thickness, M(inside));
        }

        /// <summary>
        /// One mesh with a submesh per used slot among <paramref name="include"/>, in that order; <paramref name="used"/>
        /// lists them so the caller can line up materials. Null when none of the slots has geometry.
        /// </summary>
        public Mesh Build(string name, Slot[] include, out Slot[] used)
        {
            var slots = new List<Slot>();
            var allV = new List<Vector3>(); var allN = new List<Vector3>();
            var ranges = new List<(int start, int count)>();
            foreach (var s in include)
            {
                if (!Has(s)) continue;
                slots.Add(s);
                ranges.Add((allV.Count, verts[(int)s].Count));
                allV.AddRange(verts[(int)s]); allN.AddRange(normals[(int)s]);
            }
            used = slots.ToArray();
            if (slots.Count == 0) return null;
            var mesh = new Mesh { name = name };
            if (allV.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(allV);
            mesh.SetNormals(allN);
            mesh.subMeshCount = slots.Count;
            for (int i = 0; i < ranges.Count; i++)
            {
                var idx = new int[ranges[i].count];
                for (int k = 0; k < idx.Length; k++) idx[k] = ranges[i].start + k;
                mesh.SetTriangles(idx, i, false);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        public static readonly Slot[] PaintSlots = { Slot.Paint };
        public static readonly Slot[] TrimSlots = { Slot.Dark, Slot.Glass, Slot.Head, Slot.Tail, Slot.Metal, Slot.Tire };
    }
}
