using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingGame.EditorTools.Art
{
    /// <summary>
    /// Builds Assets/CarsGame/Art/Cars/HunyuanSample/HunyuanSampleCar.prefab from the generated FBX
    /// (Tools/art, Docs/AI/ArtPipelineSample.md). The gameplay components and their tuned values are
    /// copied from Assets/Prefabs/RaceCar.prefab at bake time, so the sample drives, shoots and collides
    /// exactly like the stock car; only the visuals differ. Re-runnable: overwrites the prefab (GUID kept).
    ///
    /// Art contract checked here (throws on violation): nodes Body, Wheel_FL/FR/RL/RR (each with one
    /// spinner child), Cannon_Turret/Cannon_Barrel/Muzzle; at most 6 renderers; wheel centres within
    /// 3 cm of RaceCar's; Muzzle ahead of the turret (+Z forward); identity node rotations/scales.
    /// </summary>
    public static class HunyuanSampleCarBaker
    {
        public const string Dir = "Assets/CarsGame/Art/Cars/HunyuanSample";
        public const string ModelPath = Dir + "/Models/HunyuanSampleCar.fbx";
        public const string PrefabPath = Dir + "/HunyuanSampleCar.prefab";
        public const string PaintMaterialPath = Dir + "/Materials/M_HunyuanSample_CarPaint.mat";
        public const string TrimMaterialPath = Dir + "/Materials/M_HunyuanSample_Trim.mat";
        public const string TemplatePrefabPath = "Assets/Prefabs/RaceCar.prefab";
        public const int MaxRenderers = 6;

        private static readonly Dictionary<string, Vector3> WheelCentres = new Dictionary<string, Vector3>
        {
            { "Wheel_FL", new Vector3(-0.94f, 0.34f, 1.25f) }, { "Wheel_FR", new Vector3(0.94f, 0.34f, 1.25f) },
            { "Wheel_RL", new Vector3(-0.94f, 0.34f, -1.25f) }, { "Wheel_RR", new Vector3(0.94f, 0.34f, -1.25f) }
        };

        [MenuItem("Racing/Art/Bake Hunyuan Sample Car")]
        public static void BakeMenu() { Debug.Log(Bake()); }

        public static string Bake()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new FileNotFoundException(ModelPath + " missing — run Tools/art/run.sh first.");
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
            if (template == null) throw new FileNotFoundException(TemplatePrefabPath + " missing.");

            var paint = EnsureMaterial(PaintMaterialPath, "CarsGame/StylizedCarPaint", 1.5f);
            var trim = EnsureMaterial(TrimMaterialPath, "CarsGame/StylizedLit", 0f);

            var root = new GameObject("HunyuanSampleCar");
            var log = new StringBuilder();
            try
            {
                var imported = Object.Instantiate(model);
                // Flatten: the FBX root's children become direct children of the car root (like RaceCar's wheels).
                foreach (var child in imported.transform.Cast<Transform>().ToList())
                    child.SetParent(root.transform, true);
                Object.DestroyImmediate(imported);

                var body = Require(root.transform, "Body");
                var wheels = WheelCentres.Keys.ToDictionary(k => k, k => Require(root.transform, k));
                var turret = Require(root.transform, "Cannon_Turret");
                var barrel = Require(turret, "Cannon_Barrel");
                var muzzle = Require(barrel, "Muzzle");

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (Quaternion.Angle(t.localRotation, Quaternion.identity) > 0.5f || (t.localScale - Vector3.one).sqrMagnitude > 1e-4f)
                        throw new System.InvalidOperationException($"{t.name}: expected identity rotation/scale, got {t.localEulerAngles} / {t.localScale} (FBX axis settings?)");
                }
                foreach (var kv in wheels)
                {
                    if (kv.Value.childCount != 1) throw new System.InvalidOperationException($"{kv.Key} must have exactly one spinner child");
                    float err = Vector3.Distance(kv.Value.localPosition, WheelCentres[kv.Key]);
                    if (err > 0.03f) throw new System.InvalidOperationException($"{kv.Key} at {kv.Value.localPosition}, expected {WheelCentres[kv.Key]} (mirrored or scaled export?)");
                }
                if (muzzle.position.z <= turret.position.z) throw new System.InvalidOperationException("Muzzle is behind the turret: model faces -Z");

                // Materials, shadows, layer.
                int layer = template.layer;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    bool isBody = r.transform == body;
                    r.sharedMaterial = isBody ? paint : trim;
                    r.shadowCastingMode = isBody ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.receiveShadows = isBody;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }

                // Gameplay components: same types and values as the stock RaceCar root.
                var templateObjects = new HashSet<Object>(template.GetComponentsInChildren<Transform>(true).SelectMany(t => t.GetComponents<Component>().Cast<Object>().Append(t.gameObject)));
                foreach (var src in template.GetComponents<Component>())
                {
                    if (src == null || src is Transform) continue;
                    var dst = root.GetComponent(src.GetType()) ?? root.AddComponent(src.GetType());
                    EditorUtility.CopySerialized(src, dst);
                    ClearTemplateReferences(dst, templateObjects, log);
                }
                foreach (var t in template.GetComponentsInChildren<Transform>(true))
                {
                    if (t == template.transform) continue;
                    foreach (var c in t.GetComponents<Component>())
                        if (!(c is Transform) && !(c is MeshFilter) && !(c is Renderer))
                            log.Append($" template child component not copied: {t.name}/{c.GetType().Name};");
                }

                var car = root.GetComponent<CarController>();
                car.frontLeftWheel = wheels["Wheel_FL"]; car.frontRightWheel = wheels["Wheel_FR"];
                car.rearLeftWheel = wheels["Wheel_RL"]; car.rearRightWheel = wheels["Wheel_RR"];
                car.wheelRadius = 0.34f;
                car.playerControlled = false;
                var cannon = root.GetComponent<CarCannon>();
                cannon.barrel = barrel;          // pre-assigned => CarCannon.BuildTurret never runs (no duplicate barrel)
                cannon.muzzle = muzzle;
                cannon.turretLocalPosition = turret.localPosition;
                var carPaint = root.GetComponent<CarPaint>();
                if (carPaint == null || !paint.name.Contains(carPaint.paintMaterialNameContains))
                    throw new System.InvalidOperationException("CarPaint would not find the body material");

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok || prefab == null) throw new System.InvalidOperationException("Prefab save failed");
                return Describe(prefab) + (log.Length > 0 ? " notes:" + log : "");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Renderer/triangle/collider summary of a baked car prefab (used by the bake and by QA).</summary>
        public static string Describe(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            int tris = 0;
            var parts = new List<string>();
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                int n = 0;
                if (mf != null && mf.sharedMesh != null)
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) n += (int)mf.sharedMesh.GetIndexCount(s) / 3;
                tris += n;
                parts.Add($"{r.name}:{n}:{r.sharedMaterial?.name}");
            }
            var box = prefab.GetComponent<BoxCollider>();
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                var m = prefab.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var c = m.MultiplyPoint3x4(b.center);
                var e = b.extents;
                var wb = new Bounds(c, new Vector3(
                    Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                    Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                    Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z) * 2f);
                if (first) { bounds = wb; first = false; } else bounds.Encapsulate(wb);
            }
            if (renderers.Length > MaxRenderers) throw new System.InvalidOperationException($"{renderers.Length} renderers > {MaxRenderers}");
            return $"{AssetDatabase.GetAssetPath(prefab)}: renderers={renderers.Length} tris={tris} [{string.Join(", ", parts)}] " +
                   $"bounds min={bounds.min:F3} max={bounds.max:F3} box c={box?.center:F2} s={box?.size:F2}";
        }

        private static Transform Require(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t == null) throw new System.InvalidOperationException($"node '{name}' missing under {parent.name}");
            return t;
        }

        private static Material EnsureMaterial(string path, string shaderName, float emissionBoost)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Shader not found: " + shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Flat", 0f);                 // normals are already per-face in the FBX
            m.SetFloat("_EmissionBoost", emissionBoost);
            m.SetFloat("_EmissionByLamps", 0f);
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        // CopySerialized keeps references to the template's own children (none today); never let the
        // sample prefab point into RaceCar.prefab.
        private static void ClearTemplateReferences(Component dst, HashSet<Object> templateObjects, StringBuilder log)
        {
            var so = new SerializedObject(dst);
            var it = so.GetIterator();
            bool changed = false;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                if (!templateObjects.Contains(it.objectReferenceValue)) continue;
                log.Append($" cleared {dst.GetType().Name}.{it.propertyPath};");
                it.objectReferenceValue = null;
                changed = true;
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
