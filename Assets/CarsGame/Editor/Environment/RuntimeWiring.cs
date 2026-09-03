using UnityEditor;
using UnityEngine;
using RacingGame.World;

namespace RacingGame.EditorTools
{
    /// <summary>Scene wiring for the visual upgrade: effect materials, camera components, asset tweaks. Re-runnable.</summary>
    public static class RuntimeWiring
    {
        [MenuItem("Racing/Environment/Wire Runtime")]
        public static void Menu() { Debug.Log(Wire()); }

        public static string Wire()
        {
            var log = new System.Text.StringBuilder();
            EnvironmentBaker.Ensure("Assets/Materials"); EnvironmentBaker.Ensure(EnvironmentBaker.MatDir);

            // Dust material on our own particle shader (referenced from the scene => survives stripping).
            string dustPath = EnvironmentBaker.MatDir + "/M_Dust.mat";
            var dust = AssetDatabase.LoadAssetAtPath<Material>(dustPath);
            var particleShader = Shader.Find("CarsGame/Particle");
            if (particleShader == null) return "ERROR: CarsGame/Particle missing";
            if (dust == null) { dust = new Material(particleShader) { name = "M_Dust" }; AssetDatabase.CreateAsset(dust, dustPath); log.AppendLine("created M_Dust"); }
            else if (dust.shader != particleShader) { dust.shader = particleShader; EditorUtility.SetDirty(dust); }

            var rm = GameObject.Find("Race_Manager");
            if (rm == null) return "ERROR: Race_Manager missing";
            var assets = rm.GetComponent<WorldAssets>() ?? Undo.AddComponent<WorldAssets>(rm);
            Undo.RecordObject(assets, "assets"); assets.dustMaterial = dust; EditorUtility.SetDirty(assets);

            var cam = Camera.main;
            if (cam != null)
            {
                if (cam.GetComponent<CameraImpulse>() == null) Undo.AddComponent<CameraImpulse>(cam.gameObject);
                if (cam.GetComponent<MenuCameraDirector>() == null) Undo.AddComponent<MenuCameraDirector>(cam.gameObject);
                log.AppendLine("camera: CameraImpulse + MenuCameraDirector");
            }

            // Baked meshes are in world space: any holder must sit at world identity (gantry parent is at z=-25).
            foreach (var name in new[] { "Gantry_Mesh", "Barriers_Mesh" })
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                Undo.RecordObject(go.transform, "identity");
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var ls = go.transform.parent != null ? go.transform.parent.lossyScale : Vector3.one;
                go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
                log.AppendLine(name + " -> world identity");
            }

            // Less shadow acne on big flat facets at low sun angles.
            var pc = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pc != null) { pc.shadowDepthBias = 0.6f; pc.shadowNormalBias = 0.8f; EditorUtility.SetDirty(pc); }
            var mob = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (mob != null) { mob.shadowDepthBias = 1f; mob.shadowNormalBias = 1f; EditorUtility.SetDirty(mob); }

            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("sceneSaved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
            return log.ToString();
        }
    }
}
