using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Builds Assets/Prefabs/RaceCar.prefab from <see cref="ProceduralCarBuilder"/> using the
    /// material assets in Assets/Materials, bakes the generated meshes to Assets/Generated/Meshes
    /// and collapses each wheel's primitives into one mesh. Re-runnable: overwrites the prefab.
    /// </summary>
    public static class RaceCarPrefabBaker
    {
        public const string PrefabPath = "Assets/Prefabs/RaceCar.prefab";
        private const string MeshDir = "Assets/Generated/Meshes";

        [MenuItem("Racing/Bake RaceCar Prefab")]
        public static void BakeMenu() { Debug.Log(Bake()); }

        public static string Bake()
        {
            Material Mat(string name)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{name}.mat");
                if (m == null) throw new FileNotFoundException($"Assets/Materials/{name}.mat missing — run the material extraction first.");
                return m;
            }

            var paint = Mat("M_CarPaint_RossoCorsa");
            var carbon = Mat("M_CarbonFiber");
            var glass = Mat("M_CarGlass_Tinted");
            var tire = Mat("M_TireRubber");
            var rim = Mat("M_AlloyRim");
            var brake = Mat("M_BrakeCaliperRed");
            var head = Mat("M_Headlight_LED");
            var tail = Mat("M_Taillight_LED");
            var interior = Mat("M_Trunk");

            EnsureFolder("Assets/Generated"); EnsureFolder(MeshDir); EnsureFolder("Assets/Prefabs");

            var root = ProceduralCarBuilder.BuildSleekSportsCar(paint, carbon, glass, tire, rim, brake, head, tail, interior);
            root.name = "RaceCar";
            root.tag = "Untagged";

            var wheelPositions = new Dictionary<string, Vector3>
            {
                { "Wheel_FL", new Vector3(-0.94f, 0.34f, 1.25f) }, { "Wheel_FR", new Vector3(0.94f, 0.34f, 1.25f) },
                { "Wheel_RL", new Vector3(-0.94f, 0.34f, -1.25f) }, { "Wheel_RR", new Vector3(0.94f, 0.34f, -1.25f) }
            };
            var wheels = new Dictionary<string, Transform>();
            foreach (var kv in wheelPositions)
            {
                var pivot = ProceduralCarBuilder.CreateHighDetailWheel(kv.Key, root.transform, kv.Value, tire, rim, brake);
                wheels[kv.Key] = pivot;
                CombineChildren(pivot.Find("Spinner"), kv.Key + "_Spinner");
                CombineChildren(pivot, kv.Key + "_Hub", skipChild: "Spinner");
            }

            // Bake procedural meshes (body, canopy, aero, lights) to assets so the prefab can reference them.
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh)) continue;
                string path = $"{MeshDir}/{Sanitize(mf.gameObject.name)}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null) { AssetDatabase.DeleteAsset(path); }
                AssetDatabase.CreateAsset(mesh, path);
                mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }

            int carLayer = LayerMask.NameToLayer("Car");
            if (carLayer >= 0) foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = carLayer;

            // Mobile budget: only the body and canopy cast shadows (2 casters per car instead of 20).
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                bool caster = r.gameObject.name == "Car_Body" || r.gameObject.name == "Car_Canopy";
                r.shadowCastingMode = caster ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = caster;
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1250f;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.45f, 0.05f);
            box.size = new Vector3(1.85f, 0.70f, 4.20f);

            var car = root.AddComponent<CarController>();
            car.frontLeftWheel = wheels["Wheel_FL"]; car.frontRightWheel = wheels["Wheel_FR"];
            car.rearLeftWheel = wheels["Wheel_RL"]; car.rearRightWheel = wheels["Wheel_RR"];
            car.wheelRadius = 0.34f;
            car.playerControlled = false;

            var health = root.AddComponent<CarHealth>();
            health.showScreenBar = false; health.showWorldBar = false;
            root.AddComponent<CarCannon>();
            root.AddComponent<RaceProgress>();
            root.AddComponent<RaceParticipant>();
            root.AddComponent<CarPaint>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int renderers = prefab != null ? prefab.GetComponentsInChildren<Renderer>(true).Length : -1;
            return ok ? $"Baked {PrefabPath}: renderers={renderers}" : "Prefab save failed";
        }

        // Merge every child MeshFilter under `parent` into one mesh with one submesh per material.
        private static void CombineChildren(Transform parent, string meshName, string skipChild = null)
        {
            if (parent == null) return;
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var toDestroy = new List<GameObject>();
            foreach (var mf in parent.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.transform == parent) continue;
                if (skipChild != null && (mf.transform == parent.Find(skipChild) || mf.transform.IsChildOf(parent.Find(skipChild)))) continue;
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || mf.sharedMesh == null) continue;
                var mat = r.sharedMaterial;
                if (!byMaterial.TryGetValue(mat, out var list)) byMaterial[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = parent.worldToLocalMatrix * mf.transform.localToWorldMatrix });
                toDestroy.Add(mf.gameObject);
            }
            if (byMaterial.Count == 0) return;

            var materials = new List<Material>();
            var subMeshes = new List<CombineInstance>();
            foreach (var kv in byMaterial)
            {
                var sub = new Mesh();
                sub.CombineMeshes(kv.Value.ToArray(), true, true);
                subMeshes.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
                materials.Add(kv.Key);
            }
            var combined = new Mesh { name = meshName };
            combined.CombineMeshes(subMeshes.ToArray(), false, true);
            combined.RecalculateBounds();

            string path = $"{MeshDir}/{Sanitize(meshName)}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(combined, path);

            foreach (var go in toDestroy) Object.DestroyImmediate(go);
            var holder = new GameObject(meshName);
            holder.transform.SetParent(parent, false);
            holder.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            holder.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }
}
