using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using RacingGame.World.Gen;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Collapses the repeated authored geometry into single vertex-coloured meshes: barriers (colliders kept
    /// on renderer-less children), track dashes (transforms kept — they ARE the spline), start gantry,
    /// red/white kerbs; removes the old tree/grandstand objects (rebuilt by PropBaker) and converts the car
    /// light materials to the emissive shader. Re-runnable (skips already converted groups).
    /// </summary>
    public static class SceneConsolidator
    {
        [MenuItem("Racing/Environment/Consolidate Scene")]
        public static void Menu() { Debug.Log(Consolidate()); }

        private static readonly Color32 BarrierWhite = MeshChunkWriter.Srgb(0.90f, 0.91f, 0.93f);
        private static readonly Color32 BarrierRed = MeshChunkWriter.Srgb(0.86f, 0.14f, 0.14f);
        private static readonly Color32 DashWhite = MeshChunkWriter.Srgb(0.96f, 0.96f, 0.96f);
        private static readonly Color32 KerbRed = MeshChunkWriter.Srgb(0.88f, 0.12f, 0.14f);
        private static readonly Color32 KerbWhite = MeshChunkWriter.Srgb(0.94f, 0.94f, 0.94f);

        public static string Consolidate()
        {
            var log = new System.Text.StringBuilder();
            var rt = GameObject.Find("RaceTrack");
            if (rt == null) return "ERROR: RaceTrack missing";
            var track = Object.FindFirstObjectByType<RacingGame.TrackPath>();
            var spline = track.Spline;
            var litMat = EnvironmentBaker.EnvMaterial("M_Env_Lit", m => { m.SetFloat("_Flat", 0f); m.SetFloat("_Wrap", 0.35f); m.SetFloat("_ShadowFloor", 0.4f); m.SetFloat("_EmissionBoost", 0f); m.SetFloat("_FogOverride", 0f); m.SetFloat("_EmissionByLamps", 0f); });

            // Remember authored tree positions before anything is deleted.
            PropBaker.AuthoredTrees();

            // --- Barriers: one mesh, colliders stay on the children.
            var barriers = rt.transform.Find("Barriers");
            if (barriers != null && barriers.Find("Barriers_Mesh") == null)
            {
                var pm = PhysicsMaterialAsset();
                var w = new MeshChunkWriter("Barriers");
                int i = 0, cols = 0;
                foreach (Transform ch in barriers)
                {
                    var mf = ch.GetComponent<MeshFilter>(); var mr = ch.GetComponent<MeshRenderer>();
                    if (mf == null) continue;
                    LowPoly.Box(w, ch.localToWorldMatrix, Vector3.zero, Vector3.one, (i++ % 2 == 0) ? BarrierWhite : BarrierRed);
                    Undo.DestroyObjectImmediate(mr); Undo.DestroyObjectImmediate(mf);
                    var bc = ch.GetComponent<BoxCollider>(); if (bc != null) { bc.sharedMaterial = pm; cols++; }
                }
                var meshes = w.Finish();
                for (int k = 0; k < meshes.Count; k++)
                {
                    var saved = EnvironmentBaker.SaveMesh(meshes[k], "Barriers_" + k);
                    var go = EnvironmentBaker.Place(barriers, saved, litMat, true, true, barriers.gameObject.layer);
                    go.name = k == 0 ? "Barriers_Mesh" : "Barriers_Mesh_" + k;
                }
                Undo.RecordObject(barriers.gameObject, "activate"); barriers.gameObject.SetActive(true);
                log.AppendLine($"barriers: {i} boxes -> {meshes.Count} mesh(es), {cols} colliders, activated");
            }

            // --- Dashes: renderers removed from the spline children, mesh lives in a sibling.
            var markings = rt.transform.Find("Track_Markings");
            var decor = rt.transform.Find("Track_Decor");
            if (markings != null && decor == null)
            {
                var w = new MeshChunkWriter("Dashes");
                int n = 0;
                foreach (Transform ch in markings)
                {
                    var mf = ch.GetComponent<MeshFilter>(); var mr = ch.GetComponent<MeshRenderer>();
                    if (mf == null) continue;
                    LowPoly.Box(w, ch.localToWorldMatrix, Vector3.zero, Vector3.one, DashWhite);
                    Undo.DestroyObjectImmediate(mr); Undo.DestroyObjectImmediate(mf); n++;
                }
                var decorGo = new GameObject("Track_Decor"); Undo.RegisterCreatedObjectUndo(decorGo, "decor"); decorGo.transform.SetParent(rt.transform, false);
                foreach (var mesh in w.Finish()) EnvironmentBaker.Place(decorGo.transform, EnvironmentBaker.SaveMesh(mesh, "Dashes"), litMat, false, true);
                log.AppendLine($"dashes: {n} -> 1 mesh (spline transforms kept: {markings.childCount})");
            }

            // --- Start gantry: everything into one mesh, colliders kept.
            var gantry = rt.transform.Find("StartFinish_Gantry");
            if (gantry != null && gantry.Find("Gantry_Mesh") == null)
            {
                var w = new MeshChunkWriter("Gantry");
                int n = 0;
                foreach (var mf in gantry.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mr = mf.GetComponent<MeshRenderer>();
                    var mat = mr != null ? mr.sharedMaterial : null;
                    Color32 col = DashWhite;
                    if (mat != null && mat.HasProperty("_BaseColor")) { var c = mat.GetColor("_BaseColor"); col = MeshChunkWriter.Srgb(c.r, c.g, c.b); }
                    string meshName = mf.sharedMesh != null ? mf.sharedMesh.name : "";
                    if (meshName == "Cylinder") LowPoly.Cylinder(w, mf.transform.localToWorldMatrix, new Vector3(0f, -1f, 0f), 0.5f, 0.5f, 2f, 10, col, true);
                    else LowPoly.Box(w, mf.transform.localToWorldMatrix, Vector3.zero, Vector3.one, col);
                    Undo.DestroyObjectImmediate(mr); Undo.DestroyObjectImmediate(mf); n++;
                }
                foreach (var mesh in w.Finish()) { var go = EnvironmentBaker.Place(gantry, EnvironmentBaker.SaveMesh(mesh, "Gantry"), litMat, true, true, gantry.gameObject.layer); go.name = "Gantry_Mesh"; }
                Undo.RecordObject(gantry.gameObject, "activate"); gantry.gameObject.SetActive(true);
                log.AppendLine($"gantry: {n} parts -> 1 mesh, activated");
            }

            // --- Kerbs: red/white by distance along the spline, on the stylized shader (collider keeps the original mesh).
            foreach (var kerbName in new[] { "Track_LeftKerb", "Track_RightKerb" })
            {
                var kerb = rt.transform.Find(kerbName);
                if (kerb == null) continue;
                var mf = kerb.GetComponent<MeshFilter>(); var mr = kerb.GetComponent<MeshRenderer>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name.StartsWith("Kerb_")) continue;
                var src = mf.sharedMesh;
                var mesh = Object.Instantiate(src);
                var verts = mesh.vertices; var colors = new Color32[verts.Length];
                int hint = -1;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 wp = kerb.TransformPoint(verts[i]);
                    float d = spline.Project(wp, ref hint, 0);
                    colors[i] = (Mathf.FloorToInt(d / 3.5f) % 2 == 0) ? KerbRed : KerbWhite;
                }
                mesh.colors32 = colors;
                var saved = EnvironmentBaker.SaveMesh(mesh, kerbName.Replace("Track_", "Kerb_"));
                Undo.RecordObject(mf, "kerb"); mf.sharedMesh = saved;
                Undo.RecordObject(mr, "kerb"); mr.sharedMaterial = litMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
                log.AppendLine(kerbName + ": recoloured (" + verts.Length + " verts)");
            }

            // --- Old props: trees + grandstand are regenerated by PropBaker.
            var env = rt.transform.Find("Environment_Props");
            if (env != null) { Undo.DestroyObjectImmediate(env.gameObject); log.AppendLine("Environment_Props removed"); }

            // --- Car lights glow at night: emissive stylized shader (no vertex colours on the car => alpha 1).
            var stylized = Shader.Find("CarsGame/StylizedLit");
            foreach (var (name, baseCol) in new[] { ("M_Headlight_LED", new Color(1f, 0.96f, 0.86f)), ("M_Taillight_LED", new Color(1f, 0.12f, 0.10f)) })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + name + ".mat");
                if (m == null || m.shader == stylized) continue;
                m.shader = stylized; m.SetColor("_BaseColor", baseCol); m.SetFloat("_Wrap", 0.6f); m.SetFloat("_ShadowFloor", 0.6f);
                m.SetFloat("_EmissionBoost", 2.5f); m.SetFloat("_EmissionByLamps", 1f); m.SetFloat("_Flat", 0f); m.SetFloat("_FogOverride", 0f);
                EditorUtility.SetDirty(m); log.AppendLine(name + " -> StylizedLit emissive");
            }

            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("sceneSaved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
            return log.ToString();
        }

        private static PhysicsMaterial PhysicsMaterialAsset()
        {
            EnvironmentBaker.Ensure("Assets/Data"); EnvironmentBaker.Ensure("Assets/Data/Env");
            string path = "Assets/Data/Env/PM_Barrier.physicsMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial("PM_Barrier") { dynamicFriction = 0.2f, staticFriction = 0.2f, bounciness = 0.1f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Average };
                AssetDatabase.CreateAsset(pm, path);
            }
            return pm;
        }
    }
}
