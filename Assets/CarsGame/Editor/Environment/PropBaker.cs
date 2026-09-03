using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using RacingGame.World.Gen;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Bakes trees, tire stacks, lamp posts, the grandstand and night light pools into 64 m chunk meshes
    /// (lit / emissive / additive), plus the few colliders the cars can reach. Re-runnable.
    /// </summary>
    public static class PropBaker
    {
        private const float ChunkSize = 128f;
        private const float ShadowChunkDistance = 60f;
        private const string AuthoredTreesFile = EnvironmentBaker.MeshDir + "/authored_trees.txt";

        private class Chunk
        {
            public MeshChunkWriter Lit;
            public float MinDist = float.MaxValue;
            public Chunk(string key) { Lit = new MeshChunkWriter("Props_" + key); }
        }

        private static readonly Color32[] CanopyShades =
        {
            MeshChunkWriter.Srgb(0.24f, 0.55f, 0.25f), MeshChunkWriter.Srgb(0.32f, 0.62f, 0.27f),
            MeshChunkWriter.Srgb(0.19f, 0.47f, 0.22f), MeshChunkWriter.Srgb(0.78f, 0.56f, 0.18f)   // one autumn accent
        };
        private static readonly Color32 Trunk = MeshChunkWriter.Srgb(0.38f, 0.24f, 0.14f);
        private static readonly Color32 TireBlack = MeshChunkWriter.Srgb(0.13f, 0.13f, 0.14f);
        private static readonly Color32[] Bands = { MeshChunkWriter.Srgb(0.88f, 0.16f, 0.16f), MeshChunkWriter.Srgb(0.92f, 0.92f, 0.92f), MeshChunkWriter.Srgb(0.96f, 0.80f, 0.16f) };
        private static readonly Color32 Steel = MeshChunkWriter.Srgb(0.36f, 0.38f, 0.42f);
        private static readonly Color32 Concrete = MeshChunkWriter.Srgb(0.58f, 0.60f, 0.64f);
        private static readonly Color32 Roof = MeshChunkWriter.Srgb(0.14f, 0.28f, 0.60f);
        private static readonly Color32 LampHead = new Color32(255, 225, 170, 255);
        private static readonly Color32[] Crowd =
        {
            MeshChunkWriter.Srgb(0.90f, 0.20f, 0.20f), MeshChunkWriter.Srgb(0.20f, 0.40f, 0.90f), MeshChunkWriter.Srgb(0.95f, 0.85f, 0.20f),
            MeshChunkWriter.Srgb(0.92f, 0.92f, 0.92f), MeshChunkWriter.Srgb(0.20f, 0.70f, 0.35f), MeshChunkWriter.Srgb(0.95f, 0.55f, 0.15f),
            MeshChunkWriter.Srgb(0.15f, 0.15f, 0.18f), MeshChunkWriter.Srgb(0.70f, 0.30f, 0.75f)
        };

        [MenuItem("Racing/Environment/Bake Props")]
        public static void Menu() { Debug.Log(BakeProps()); }

        /// <summary>Authored tree positions: from the scene's Environment_Props if it still exists, else the cached file.</summary>
        public static List<Vector3> AuthoredTrees()
        {
            var list = new List<Vector3>();
            var rt = GameObject.Find("RaceTrack");
            var env = rt != null ? rt.transform.Find("Environment_Props") : null;
            if (env != null)
            {
                foreach (Transform ch in env) if (ch.name.StartsWith("Tree_")) list.Add(ch.position);
                if (list.Count > 0)
                {
                    EnvironmentBaker.Ensure("Assets/Generated"); EnvironmentBaker.Ensure(EnvironmentBaker.MeshDir);
                    var sb = new System.Text.StringBuilder();
                    foreach (var p in list) sb.AppendLine(p.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " " + p.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    File.WriteAllText(AuthoredTreesFile, sb.ToString());
                    return list;
                }
            }
            if (File.Exists(AuthoredTreesFile))
                foreach (var line in File.ReadAllLines(AuthoredTreesFile))
                {
                    var parts = line.Split(' ');
                    if (parts.Length == 2) list.Add(new Vector3(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), 0f, float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)));
                }
            return list;
        }

        public static string BakeProps()
        {
            var log = new System.Text.StringBuilder();
            var track = Object.FindFirstObjectByType<RacingGame.TrackPath>();
            if (track == null) return "ERROR: TrackPath not found";
            var spline = track.Spline;

            Mesh trunkMesh = null, canopyMesh = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/StylizedTreeMeshes.fbx"))
                if (o is Mesh m) { if (m.name == "StylizedTreeTrunk") trunkMesh = m; else if (m.name == "StylizedTreeCanopy") canopyMesh = m; }
            if (trunkMesh == null || canopyMesh == null) return "ERROR: tree meshes not found in StylizedTreeMeshes.fbx";

            var litMat = EnvironmentBaker.EnvMaterial("M_Env_Lit", m => { m.SetFloat("_Flat", 0f); m.SetFloat("_Wrap", 0.35f); m.SetFloat("_ShadowFloor", 0.4f); m.SetFloat("_EmissionBoost", 0f); m.SetFloat("_FogOverride", 0f); m.SetFloat("_EmissionByLamps", 0f); });
            var glowMat = EnvironmentBaker.EnvMaterial("M_Env_Emissive", m => { m.SetFloat("_Flat", 0f); m.SetFloat("_Wrap", 0.5f); m.SetFloat("_ShadowFloor", 0.5f); m.SetFloat("_EmissionBoost", 3f); m.SetFloat("_EmissionByLamps", 1f); m.SetFloat("_FogOverride", 0f); });
            var poolMat = AdditiveMaterial("M_LightPool");

            var authored = AuthoredTrees();
            var layout = PropLayout.Build(spline, authored, track.startLineDistance, 4242);
            log.AppendLine($"layout: trees={layout.Trees.Count} (authored {authored.Count}) tireStacks={layout.TireStacks.Count} lamps={layout.Lamps.Count}");

            var chunks = new Dictionary<(int, int), Chunk>();
            Chunk ChunkAt(Vector3 p, float distToTrack)
            {
                var key = (Mathf.FloorToInt(p.x / ChunkSize), Mathf.FloorToInt(p.z / ChunkSize));
                if (!chunks.TryGetValue(key, out var c)) chunks[key] = c = new Chunk(key.Item1 + "_" + key.Item2);
                c.MinDist = Mathf.Min(c.MinDist, distToTrack);
                return c;
            }
            var colliders = new List<(Vector3 pos, float radius, float height)>();
            var glow = new MeshChunkWriter("PropsGlow");     // all emissive heads in one small mesh (one draw)

            // Trees
            foreach (var t in layout.Trees)
            {
                var c = ChunkAt(t.Position, t.DistToTrack);
                var rot = Quaternion.Euler(0f, t.Yaw, 0f);
                float s = t.Scale;
                c.Lit.AddMesh(trunkMesh, Matrix4x4.TRS(t.Position + Vector3.up * 1.6f * s, rot, new Vector3(0.6f, 1.6f, 0.6f) * s), Trunk, true);
                float cw = 4.6f * s * (0.92f + 0.16f * ((t.Shade * 37) % 7) / 6f);
                c.Lit.AddMesh(canopyMesh, Matrix4x4.TRS(t.Position + Vector3.up * 4.2f * s, rot, new Vector3(cw, 5.3f * s, cw)), CanopyShades[t.Shade], true);
                if (t.DistToTrack < PropLayout.ColliderTreeDistance) colliders.Add((t.Position + Vector3.up * 1.6f * s, 0.45f * s, 3.2f * s));
            }

            // Tire stacks
            foreach (var ts in layout.TireStacks)
            {
                var c = ChunkAt(ts.Position, 8f);
                for (int i = 0; i < 3; i++)
                    LowPoly.Cylinder(c.Lit, Matrix4x4.identity, ts.Position + Vector3.up * (i * 0.31f), 0.56f, 0.56f, 0.28f, 10, i == 1 ? Bands[ts.BandColor] : TireBlack, true);
                colliders.Add((ts.Position + Vector3.up * 0.5f, 0.6f, 1.0f));
            }

            // Lamp posts + light pools
            var pools = new MeshChunkWriter("LightPools");
            foreach (var l in layout.Lamps)
            {
                var c = ChunkAt(l.Position, l.LateralToRoad);
                LowPoly.Cylinder(c.Lit, Matrix4x4.identity, l.Position, 0.13f, 0.09f, 6.5f, 6, Steel, false);
                var armRot = Quaternion.LookRotation(l.TowardsTrack, Vector3.up);
                var armM = Matrix4x4.TRS(l.Position + Vector3.up * 6.4f, armRot, Vector3.one);
                LowPoly.Box(c.Lit, armM, new Vector3(0f, 0f, 0.9f), new Vector3(0.12f, 0.12f, 1.8f), Steel);
                LowPoly.Box(glow, armM, new Vector3(0f, -0.1f, 1.75f), new Vector3(0.75f, 0.16f, 0.4f), LampHead);
                // Soft pool on the asphalt: fan with bright centre and transparent rim.
                Vector3 centre = l.Position + l.TowardsTrack * (l.LateralToRoad - 3.2f) + Vector3.up * 0.07f;
                Fan(pools, centre, l.Along, l.TowardsTrack, 7.5f, 5.5f, new Color32(255, 200, 130, 120));
            }

            // Grandstand
            {
                var g = layout.Grandstand;
                var c = ChunkAt(g.Position, 15f);
                var rot = Quaternion.LookRotation(g.Forward, Vector3.up);
                var m = Matrix4x4.TRS(g.Position, rot, Vector3.one);
                var rnd = new System.Random(99);
                int rows = 6;
                for (int r = 0; r < rows; r++)
                {
                    float z = -r * 1.4f, y = r * 0.85f;
                    LowPoly.Box(c.Lit, m, new Vector3(0f, y + 0.425f, z), new Vector3(g.Length, 0.85f, 1.4f), Concrete);
                    for (float x = -g.Length * 0.5f + 0.7f; x < g.Length * 0.5f - 0.5f; x += 1.05f)
                    {
                        if (rnd.NextDouble() > 0.86) continue;
                        var col = Crowd[rnd.Next(Crowd.Length)];
                        float h = 0.6f + (float)rnd.NextDouble() * 0.35f;
                        LowPoly.Box(c.Lit, m, new Vector3(x, y + 0.85f + h * 0.5f, z - 0.25f), new Vector3(0.5f, h, 0.5f), col);
                    }
                }
                // roof on posts
                for (int i = 0; i < 5; i++)
                    LowPoly.Cylinder(c.Lit, m, new Vector3(-g.Length * 0.5f + 2f + i * (g.Length - 4f) / 4f, 0f, -rows * 1.4f - 0.6f), 0.18f, 0.18f, 8.5f, 6, Steel, false);
                var roofM = m * Matrix4x4.TRS(new Vector3(0f, 8.6f, -rows * 0.7f), Quaternion.Euler(-9f, 0f, 0f), Vector3.one);
                LowPoly.Box(c.Lit, roofM, Vector3.zero, new Vector3(g.Length + 2f, 0.3f, rows * 1.4f + 3f), Roof);
                // back wall
                LowPoly.Box(c.Lit, m, new Vector3(0f, rows * 0.85f * 0.5f, -rows * 1.4f + 0.2f), new Vector3(g.Length, rows * 0.85f, 0.3f), Concrete);
            }

            // Write chunk meshes
            var propsGroup = EnvironmentBaker.Group("Props");
            int meshCount = 0, verts = 0;
            foreach (var kv in chunks)
            {
                bool casts = kv.Value.MinDist < ShadowChunkDistance;
                int i = 0;
                foreach (var mesh in kv.Value.Lit.Finish())
                {
                    verts += mesh.vertexCount;
                    var saved = EnvironmentBaker.SaveMesh(mesh, $"Props_{kv.Key.Item1}_{kv.Key.Item2}_{i++}");
                    EnvironmentBaker.Place(propsGroup, saved, litMat, casts, true); meshCount++;
                }
            }
            foreach (var mesh in glow.Finish())
            {
                var saved = EnvironmentBaker.SaveMesh(mesh, "PropsGlow");
                EnvironmentBaker.Place(propsGroup, saved, glowMat, false, false); meshCount++;
            }
            foreach (var mesh in pools.Finish())
            {
                var saved = EnvironmentBaker.SaveMesh(mesh, "LightPools");
                EnvironmentBaker.Place(propsGroup, saved, poolMat, false, false); meshCount++;
            }
            log.AppendLine($"props: {meshCount} renderers, {verts} verts, chunks={chunks.Count}");

            // Colliders (Decoration layer, no renderers)
            var colGroup = EnvironmentBaker.Group("Colliders");
            int decoLayer = LayerMask.NameToLayer("Decoration"); if (decoLayer < 0) decoLayer = 0;
            foreach (var (pos, radius, height) in colliders)
            {
                var go = new GameObject("Col");
                go.transform.SetParent(colGroup, false); go.transform.position = pos; go.layer = decoLayer;
                var cap = go.AddComponent<CapsuleCollider>(); cap.radius = radius; cap.height = height; cap.direction = 1;
            }
            log.AppendLine($"colliders: {colliders.Count} on layer {decoLayer}");

            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("sceneSaved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
            return log.ToString();
        }

        private static void Fan(MeshChunkWriter w, Vector3 centre, Vector3 along, Vector3 across, float halfAlong, float halfAcross, Color32 colour)
        {
            const int n = 12;
            var rim = colour; rim.a = 0;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 p0 = centre + along * (Mathf.Cos(a0) * halfAlong) + across * (Mathf.Sin(a0) * halfAcross);
                Vector3 p1 = centre + along * (Mathf.Cos(a1) * halfAlong) + across * (Mathf.Sin(a1) * halfAcross);
                w.AddTriangle(centre, p0, p1, colour, rim, rim);
            }
        }

        public static Material AdditiveMaterial(string name)
        {
            EnvironmentBaker.Ensure("Assets/Materials"); EnvironmentBaker.Ensure(EnvironmentBaker.MatDir);
            string path = $"{EnvironmentBaker.MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("CarsGame/Additive");
            if (shader == null) throw new System.InvalidOperationException("CarsGame/Additive missing");
            if (m == null) { m = new Material(shader) { name = name }; m.SetFloat("_Intensity", 1f); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) { m.shader = shader; EditorUtility.SetDirty(m); }
            return m;
        }
    }
}
