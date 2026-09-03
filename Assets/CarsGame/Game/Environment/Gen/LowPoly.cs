using UnityEngine;

namespace RacingGame.World.Gen
{
    /// <summary>Flat-shaded primitives written straight into a <see cref="MeshChunkWriter"/>.</summary>
    public static class LowPoly
    {
        /// <summary>Axis-aligned box in local space transformed by m. Faces get slightly different shades for the low-poly look.</summary>
        public static void Box(MeshChunkWriter w, Matrix4x4 m, Vector3 centre, Vector3 size, Color32 color)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => m.MultiplyPoint3x4(centre + new Vector3(x * h.x, y * h.y, z * h.z));
            var top = color; var side = MeshChunkWriter.Scale(color, 0.92f); var bottom = MeshChunkWriter.Scale(color, 0.8f);
            w.AddQuad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), top);            // +Y
            w.AddQuad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), bottom);      // -Y
            w.AddQuad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), side);        // -Z
            w.AddQuad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), side);            // +Z
            w.AddQuad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), side);        // -X
            w.AddQuad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), side);            // +X
        }

        /// <summary>Cylinder along local Y from y0 to y1 (radius may differ at the ends: cones/frustums).</summary>
        public static void Cylinder(MeshChunkWriter w, Matrix4x4 m, Vector3 basePos, float r0, float r1, float height, int sides, Color32 color, bool caps = true)
        {
            Vector3 P(float ang, float r, float y) => m.MultiplyPoint3x4(basePos + new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r));
            float step = Mathf.PI * 2f / sides;
            Vector3 top = m.MultiplyPoint3x4(basePos + Vector3.up * height), bottom = m.MultiplyPoint3x4(basePos);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step;
                Vector3 b0 = P(a0, r0, 0f), b1 = P(a1, r0, 0f), t0 = P(a0, r1, height), t1 = P(a1, r1, height);
                var shade = MeshChunkWriter.Scale(color, 0.88f + 0.12f * ((i & 1) == 0 ? 1f : 0.6f));
                if (r1 > 0.001f) w.AddQuad(b1, t1, t0, b0, shade); else w.AddTriangle(b1, top, b0, shade);
                if (caps)
                {
                    if (r1 > 0.001f) w.AddTriangle(t0, t1, top, color);
                    if (r0 > 0.001f) w.AddTriangle(b1, b0, bottom, MeshChunkWriter.Scale(color, 0.8f));
                }
            }
        }

        /// <summary>Tapered pyramid (rock/roof piece): base rectangle at y=0, apex offset.</summary>
        public static void Pyramid(MeshChunkWriter w, Matrix4x4 m, Vector3 basePos, Vector2 size, float height, Vector2 apexOffset, Color32 color)
        {
            Vector3 P(float x, float y, float z) => m.MultiplyPoint3x4(basePos + new Vector3(x, y, z));
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            Vector3 a = P(-hx, 0, -hz), b = P(hx, 0, -hz), c = P(hx, 0, hz), d = P(-hx, 0, hz), apex = P(apexOffset.x, height, apexOffset.y);
            w.AddTriangle(a, apex, b, color); w.AddTriangle(b, apex, c, MeshChunkWriter.Scale(color, 0.9f));
            w.AddTriangle(c, apex, d, MeshChunkWriter.Scale(color, 0.85f)); w.AddTriangle(d, apex, a, MeshChunkWriter.Scale(color, 0.95f));
        }

        /// <summary>Flat quad on the ground (light pools, patches). Normal up.</summary>
        public static void GroundQuad(MeshChunkWriter w, Vector3 centre, Vector3 forward, float halfWidth, float halfLength, Color32 color, float y)
        {
            forward.y = 0f; forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 c = new Vector3(centre.x, y, centre.z);
            w.AddQuad(c - right * halfWidth - forward * halfLength, c - right * halfWidth + forward * halfLength, c + right * halfWidth + forward * halfLength, c + right * halfWidth - forward * halfLength, color);
        }
    }
}
