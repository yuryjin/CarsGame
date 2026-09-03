using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingGame.World.Gen
{
    /// <summary>
    /// Accumulates coloured triangles and instanced meshes into UInt16 meshes (auto-split below 65k verts).
    /// Vertex colours are sRGB bytes decoded by CarsGame/StylizedLit; alpha carries the emission/horizon mask.
    /// </summary>
    public sealed class MeshChunkWriter
    {
        private readonly string baseName;
        private readonly List<Mesh> finished = new List<Mesh>();
        private List<Vector3> verts = new List<Vector3>();
        private List<Vector3> normals = new List<Vector3>();
        private List<Color32> colors = new List<Color32>();
        private List<int> tris = new List<int>();
        private const int Limit = 65000;

        public MeshChunkWriter(string baseName) { this.baseName = baseName; }

        public int PendingVertices => verts.Count;

        private void EnsureRoom(int extraVerts) { if (verts.Count + extraVerts > Limit) Flush(); }

        /// <summary>Flat-shaded triangle (3 split vertices, face normal).</summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color32 ca, Color32 cb, Color32 cc)
        {
            EnsureRoom(3);
            Vector3 n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(ca); colors.Add(cb); colors.Add(cc);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color32 color) => AddTriangle(a, b, c, color, color, color);

        /// <summary>Quad a-b-c-d (counter-clockwise seen from the front), two flat triangles.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            AddTriangle(a, b, c, color);
            AddTriangle(a, c, d, color);
        }

        /// <summary>Copy a mesh (all submeshes) with a transform and a tint; keeps the source normals (smooth trees etc.).</summary>
        public void AddMesh(Mesh src, Matrix4x4 m, Color32 tint, bool flatShade = false)
        {
            var sv = src.vertices; var sn = src.normals; var st = src.triangles;
            if (flatShade || sn == null || sn.Length != sv.Length)
            {
                for (int t = 0; t < st.Length; t += 3)
                    AddTriangle(m.MultiplyPoint3x4(sv[st[t]]), m.MultiplyPoint3x4(sv[st[t + 1]]), m.MultiplyPoint3x4(sv[st[t + 2]]), tint);
                return;
            }
            EnsureRoom(sv.Length);
            int baseIndex = verts.Count;
            var nm = m.inverse.transpose;
            for (int i = 0; i < sv.Length; i++)
            {
                verts.Add(m.MultiplyPoint3x4(sv[i]));
                normals.Add(nm.MultiplyVector(sn[i]).normalized);
                colors.Add(tint);
            }
            for (int t = 0; t < st.Length; t++) tris.Add(baseIndex + st[t]);
        }

        public void Flush()
        {
            if (verts.Count == 0) return;
            var mesh = new Mesh { name = baseName + "_" + finished.Count, indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateBounds();
            finished.Add(mesh);
            verts = new List<Vector3>(); normals = new List<Vector3>(); colors = new List<Color32>(); tris = new List<int>();
        }

        public List<Mesh> Finish() { Flush(); return finished; }

        public static Color32 Srgb(float r, float g, float b, float a = 0f) =>
            new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(b) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));

        public static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));

        public static Color32 Scale(Color32 c, float k) => new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(c.r * k), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * k), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * k), 0, 255), c.a);
    }
}
