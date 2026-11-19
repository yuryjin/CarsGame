using System.Collections.Generic;
using System.IO;
using RacingGame.World.Gen;
using UnityEditor;
using UnityEngine;

namespace RacingGame.EditorTools
{
    internal static class CircuitSurfaceBaker
    {
        internal static void BakeAsphalt(TrackPath track, MeshFilter road)
        {
            var source = road.GetComponent<MeshCollider>().sharedMesh;
            var vertices = new List<Vector3>(source.vertices);
            var normals = new List<Vector3>(source.normals);
            int end = vertices.Count;
            // The closing quad needs its own UVs at the end of the lap. Keep the authored collision mesh.
            vertices.Add(vertices[0]); vertices.Add(vertices[1]);
            normals.Add(normals[0]); normals.Add(normals[1]);
            var triangles = source.triangles;
            for (int i = triangles.Length - 6; i < triangles.Length; i++)
                if (triangles[i] < 2) triangles[i] += end;

            var uv = new Vector2[vertices.Count];
            int hint = -1;
            for (int i = 0; i < end; i += 2)
            {
                var centre = road.transform.TransformPoint((vertices[i] + vertices[i + 1]) * 0.5f);
                float v = track.Spline.Project(centre, ref hint) / track.Length;
                uv[i] = new Vector2(0f, v);
                uv[i + 1] = new Vector2(1f, v);
            }
            uv[end] = new Vector2(0f, 1f); uv[end + 1] = Vector2.one;
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.uv = uv;
            mesh.triangles = triangles; mesh.RecalculateTangents();
            road.sharedMesh = EnvironmentBaker.SaveMesh(mesh, "CircuitAsphalt", ModelImporterMeshCompression.Off);

            float width = Vector3.Distance(road.transform.TransformPoint(vertices[0]), road.transform.TransformPoint(vertices[1]));
            var tiling = new Vector2(3f, Mathf.Round(track.Length * 3f / width));
            var material = road.GetComponent<MeshRenderer>().sharedMaterial;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Env/CircuitAsphalt.png"));
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTexture("_DetailAlbedoMap", BakeRubber(track, width));
            // URP applies the base tiling first. The wear map spans exactly one lap.
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(1f / tiling.x, 1f / tiling.y));
            material.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(material);
        }

        private static Texture2D BakeRubber(TrackPath track, float roadWidth)
        {
            const int width = 128, height = 1024;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float d = (y + 0.5f) * track.Length / height;
                float lane = track.Profile.LaneAt(d);
                float pressure = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 1.65f, Mathf.Abs(lane)));
                for (int x = 0; x < width; x++)
                {
                    float offset = ((x + 0.5f) / width - 0.5f) * roadWidth - lane;
                    float sweep = Mathf.Exp(-offset * offset / 2.4f);
                    float wheel = (Mathf.Abs(offset) - 0.72f) / 0.33f;
                    float wear = pressure * (0.30f * sweep + 0.15f * Mathf.Exp(-wheel * wheel));
                    byte tone = (byte)Mathf.RoundToInt(128f * (1f - wear));
                    pixels[y * width + x] = new Color32(tone, tone, tone, 255);
                }
            }
            const string path = EnvironmentBaker.MeshDir + "/CircuitRubber.png";
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false, true);
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.wrapModeU = TextureWrapMode.Clamp;
            importer.wrapModeV = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Mesh Kerb(Mesh source)
        {
            var writer = new MeshChunkWriter("Kerb");
            var vertices = source.vertices;
            var red = new Color32(190, 61, 46, 0);
            var white = new Color32(227, 221, 204, 0);
            for (int i = 0; i < vertices.Length; i += 2)
            {
                int next = (i + 2) % vertices.Length;
                var color = i % 4 == 0 ? red : white;
                // Split at the paint boundary but retain the source diagonal for the fork openings.
                writer.AddQuad(vertices[next], vertices[next + 1], vertices[i + 1], vertices[i], color);
            }
            return writer.Finish()[0];
        }

        internal static void AddGridPaint(MeshChunkWriter writer, TrackPath track, StartGrid grid, Transform checkers)
        {
            int hint = -1;
            float line = track.Spline.Project(checkers.position, ref hint);
            var paint = new Color32(194, 192, 180, 0);
            for (int row = 0; row < 4; row++)
            {
                float d = line - grid.poleSetback - row * grid.rowSpacing;
                track.Pose(d, 0f, out var centre, out var rotation);
                var right = rotation * Vector3.right;
                var forward = rotation * Vector3.forward;
                for (int column = -1; column <= 1; column += 2)
                {
                    var front = centre + right * (column * grid.columnOffset) + forward * 2f;
                    void Rect(float left, float back, float width, float length)
                    {
                        var corner = front + right * left + forward * back;
                        writer.AddQuad(corner, corner + forward * length,
                            corner + forward * length + right * width, corner + right * width, paint);
                    }
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Rect(side * 1.12f - 0.06f, -0.94f, 0.12f, 0.82f);
                        Rect(side * 0.81f - 0.37f, -0.12f, 0.74f, 0.12f);
                    }
                }
            }
        }
    }
}
