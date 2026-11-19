using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using RacingGame.World.Gen;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Bakes the generated environment (terrain, mountains, later props) into mesh assets under
    /// Assets/Generated/Env and scene objects under a root "Environment". Re-runnable: replaces its own output.
    /// The physics ground stays the original Ground_Terrain plane (only its renderer is disabled).
    /// </summary>
    public static class EnvironmentBaker
    {
        public const string MeshDir = "Assets/Generated/Env";
        public const string MatDir = "Assets/Materials/Env";
        public static readonly Vector2 Centre = new Vector2(5f, 0f);

        [MenuItem("Racing/Environment/Bake Terrain + Mountains")]
        public static void Menu() { Debug.Log(BakeTerrain()); }

        public static Material EnvMaterial(string name, System.Action<Material> setup)
        {
            Ensure("Assets/Materials"); Ensure(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("CarsGame/StylizedLit");
            if (shader == null) throw new System.InvalidOperationException("CarsGame/StylizedLit missing");
            if (m == null) { m = new Material(shader) { name = name }; setup(m); AssetDatabase.CreateAsset(m, path); }
            else { if (m.shader != shader) m.shader = shader; setup(m); EditorUtility.SetDirty(m); }
            return m;
        }

        public static GameObject Root()
        {
            var root = GameObject.Find("Environment");
            if (root == null) { root = new GameObject("Environment"); Undo.RegisterCreatedObjectUndo(root, "Environment"); }
            return root;
        }

        public static Transform Group(string name)
        {
            var root = Root().transform;
            var t = root.Find(name);
            if (t != null) { Undo.DestroyObjectImmediate(t.gameObject); }
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        public static Mesh SaveMesh(Mesh mesh, string name, ModelImporterMeshCompression compression = ModelImporterMeshCompression.Medium)
        {
            Ensure("Assets/Generated"); Ensure(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            mesh.name = name;
            MeshUtility.SetMeshCompression(mesh, compression);
            mesh.UploadMeshData(false);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        public static GameObject Place(Transform parent, Mesh mesh, Material mat, bool castShadows, bool receiveShadows, int layer = 0)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // vertices are baked in world space
            go.transform.localScale = parent.lossyScale.x != 0f ? new Vector3(1f / parent.lossyScale.x, 1f / parent.lossyScale.y, 1f / parent.lossyScale.z) : Vector3.one;
            go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        public static string BakeTerrain()
        {
            var log = new System.Text.StringBuilder();
            var track = Object.FindFirstObjectByType<RacingGame.TrackPath>();
            if (track == null) return "ERROR: TrackPath not found";
            var spline = track.Spline;

            var terrainMat = EnvMaterial("M_Env_Terrain", m => { m.SetFloat("_Flat", 1f); m.SetFloat("_Wrap", 0.4f); m.SetFloat("_ShadowFloor", 0.45f); m.SetFloat("_EmissionBoost", 0f); m.SetFloat("_FogOverride", 0f); });
            var mountainMat = EnvMaterial("M_Mountains", m => { m.SetFloat("_Flat", 1f); m.SetFloat("_Wrap", 0.5f); m.SetFloat("_ShadowFloor", 0.6f); m.SetFloat("_FogOverride", 1f); m.SetFloat("_EmissionBoost", 0f); });

            var settings = new TerrainSettings();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var meshes = TerrainMeshGen.Build(spline, settings, Centre);
            log.AppendLine($"terrain: {meshes.Count} chunks in {sw.ElapsedMilliseconds} ms");
            var terrainGroup = Group("Terrain");
            int verts = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                verts += meshes[i].vertexCount;
                var saved = SaveMesh(meshes[i], "Terrain_" + i);
                Place(terrainGroup, saved, terrainMat, false, true);
            }
            log.AppendLine("terrain verts=" + verts);

            var mountainGroup = Group("Mountains");
            var foothills = SaveMesh(MountainRingGen.Build("Mountains_Foothills", Centre, 570f, 160, 12f, 60f, 40f, 76, 0.45f, 0.8f), "Mountains_Foothills");
            var near = SaveMesh(MountainRingGen.Build("Mountains_Near", Centre, 690f, 200, 50f, 190f, 70f, 77, 0.85f), "Mountains_Near");
            var far = SaveMesh(MountainRingGen.Build("Mountains_Far", Centre, 860f, 180, 120f, 300f, 90f, 78, 0.65f, 0.7f), "Mountains_Far");
            Place(mountainGroup, far, mountainMat, false, false);
            Place(mountainGroup, near, mountainMat, false, false);
            Place(mountainGroup, foothills, mountainMat, false, false);

            var ground = GameObject.Find("Ground_Terrain");
            if (ground != null) { var r = ground.GetComponent<MeshRenderer>(); if (r != null) { Undo.RecordObject(r, "hide plane"); r.enabled = false; } }

            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("sceneSaved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
            return log.ToString();
        }

        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
