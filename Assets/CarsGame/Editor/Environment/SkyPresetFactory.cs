using UnityEditor;
using UnityEngine;
using RacingGame.World;

namespace RacingGame.EditorTools
{
    /// <summary>Creates/updates the five SkyPreset assets, the sky material, wires SkyDirector and the events. Re-runnable.</summary>
    public static class SkyPresetFactory
    {
        private const string Dir = "Assets/Data/Sky";
        private const string MatDir = "Assets/Materials/Env";

        [MenuItem("Racing/Environment/Create Sky Presets")]
        public static void Menu() { Debug.Log(CreateAll()); }

        public static string CreateAll()
        {
            var log = new System.Text.StringBuilder();
            Ensure("Assets/Data"); Ensure(Dir); Ensure("Assets/Materials"); Ensure(MatDir);

            var skyShader = Shader.Find("CarsGame/Sky");
            if (skyShader == null) return "ERROR: shader CarsGame/Sky not found (compile errors?)";
            string matPath = MatDir + "/M_Sky.mat";
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (skyMat == null) { skyMat = new Material(skyShader) { name = "M_Sky" }; AssetDatabase.CreateAsset(skyMat, matPath); log.AppendLine("created " + matPath); }
            else if (skyMat.shader != skyShader) { skyMat.shader = skyShader; EditorUtility.SetDirty(skyMat); }

            var day = Preset("Sky_Day", p =>
            {
                p.zenith = C(0.20f, 0.45f, 0.90f); p.horizon = C(0.80f, 0.89f, 0.97f); p.ground = C(0.32f, 0.35f, 0.38f); p.horizonExp = 0.55f;
                p.sunEuler = new Vector3(48f, 325f, 0f); p.sunLightColor = C(1f, 0.96f, 0.90f); p.sunIntensity = 1.5f;
                p.sunDiscColor = new Color(2.5f, 2.2f, 1.6f); p.sunAngularRadius = 1.4f; p.sunGlow = 0.3f; p.sunGlowPower = 24f;
                p.moonColor = Color.black; p.stars = 0f;
                p.fogStart = 120f; p.fogEnd = 420f;
                p.ambientSky = C(0.55f, 0.66f, 0.88f); p.ambientEquator = C(0.60f, 0.62f, 0.60f); p.ambientGround = C(0.22f, 0.20f, 0.16f);
                p.mountainColor = C(0.42f, 0.50f, 0.66f);
                p.postExposure = 0.15f; p.saturation = 18f; p.contrast = 14f; p.colorFilter = Color.white; p.bloomIntensity = 0.7f; p.vignette = 0.28f;
                p.headlights = false; p.lampsOn = false;
            }, log);
            var morning = Preset("Sky_Morning", p =>
            {
                p.zenith = C(0.35f, 0.55f, 0.85f); p.horizon = C(0.98f, 0.82f, 0.62f); p.ground = C(0.35f, 0.32f, 0.30f); p.horizonExp = 0.5f;
                p.sunEuler = new Vector3(24f, 300f, 0f); p.sunLightColor = C(1f, 0.85f, 0.68f); p.sunIntensity = 1.3f;
                p.sunDiscColor = new Color(3f, 2f, 1.2f); p.sunAngularRadius = 1.8f; p.sunGlow = 0.4f; p.sunGlowPower = 18f;
                p.moonColor = Color.black; p.stars = 0f;
                p.fogStart = 100f; p.fogEnd = 380f;
                p.ambientSky = C(0.60f, 0.60f, 0.75f); p.ambientEquator = C(0.75f, 0.62f, 0.50f); p.ambientGround = C(0.25f, 0.20f, 0.16f);
                p.mountainColor = C(0.62f, 0.55f, 0.62f);
                p.postExposure = 0.1f; p.saturation = 12f; p.contrast = 12f; p.colorFilter = C(1f, 0.97f, 0.92f); p.bloomIntensity = 0.9f; p.vignette = 0.3f;
            }, log);
            var sunset = Preset("Sky_Sunset", p =>
            {
                p.zenith = C(0.22f, 0.18f, 0.46f); p.horizon = C(1.0f, 0.52f, 0.26f); p.ground = C(0.30f, 0.20f, 0.20f); p.horizonExp = 0.45f;
                p.sunEuler = new Vector3(22f, 250f, 0f); p.sunLightColor = C(1f, 0.65f, 0.40f); p.sunIntensity = 1.1f;
                p.sunDiscColor = new Color(3f, 1.6f, 0.8f); p.sunAngularRadius = 2.4f; p.sunGlow = 0.45f; p.sunGlowPower = 20f;
                p.moonColor = Color.black; p.stars = 0.15f;
                p.fogStart = 90f; p.fogEnd = 350f;
                p.ambientSky = C(0.45f, 0.35f, 0.60f); p.ambientEquator = C(0.80f, 0.45f, 0.30f); p.ambientGround = C(0.25f, 0.15f, 0.12f);
                p.mountainColor = C(0.45f, 0.28f, 0.42f);
                p.postExposure = 0.1f; p.saturation = 25f; p.contrast = 18f; p.colorFilter = C(1f, 0.95f, 0.9f); p.bloomIntensity = 1.0f; p.vignette = 0.35f;
                p.lampsOn = true;
            }, log);
            var night = Preset("Sky_Night", p =>
            {
                // Keep the driving line and car silhouette visible in the unlit sections of the circuit.
                p.zenith = C(0.02f, 0.03f, 0.08f); p.horizon = C(0.10f, 0.14f, 0.26f); p.ground = C(0.03f, 0.03f, 0.05f); p.horizonExp = 0.6f;
                p.sunEuler = new Vector3(50f, 120f, 0f); p.sunLightColor = C(0.60f, 0.70f, 1f); p.sunIntensity = 0.8f;
                p.sunDiscColor = Color.black; p.sunGlow = 0f;
                p.moonEuler = new Vector3(50f, 120f, 0f); p.moonColor = C(0.9f, 0.95f, 1f); p.moonAngularRadius = 1.6f; p.stars = 1f;
                p.fogStart = 80f; p.fogEnd = 300f;
                p.ambientSky = C(0.32f, 0.38f, 0.55f); p.ambientEquator = C(0.24f, 0.29f, 0.42f); p.ambientGround = C(0.13f, 0.16f, 0.24f);
                p.mountainColor = C(0.06f, 0.08f, 0.14f);
                p.postExposure = 0.5f; p.saturation = 10f; p.contrast = 5f; p.colorFilter = C(0.92f, 0.95f, 1f); p.bloomIntensity = 1.2f; p.vignette = 0.25f;
                p.headlights = true; p.lampsOn = true;
            }, log);
            var overcast = Preset("Sky_Overcast", p =>
            {
                p.zenith = C(0.55f, 0.60f, 0.66f); p.horizon = C(0.80f, 0.82f, 0.84f); p.ground = C(0.35f, 0.36f, 0.37f); p.horizonExp = 0.5f;
                p.sunEuler = new Vector3(60f, 340f, 0f); p.sunLightColor = C(0.95f, 0.97f, 1f); p.sunIntensity = 0.9f;
                p.sunDiscColor = new Color(1.2f, 1.2f, 1.2f); p.sunAngularRadius = 3f; p.sunGlow = 0.4f; p.sunGlowPower = 6f;
                p.moonColor = Color.black; p.stars = 0f;
                p.fogStart = 60f; p.fogEnd = 260f;
                p.ambientSky = C(0.60f, 0.63f, 0.68f); p.ambientEquator = C(0.55f, 0.56f, 0.58f); p.ambientGround = C(0.25f, 0.25f, 0.25f);
                p.mountainColor = C(0.55f, 0.58f, 0.62f);
                p.postExposure = 0.05f; p.saturation = -5f; p.contrast = 8f; p.colorFilter = Color.white; p.bloomIntensity = 0.4f; p.vignette = 0.3f;
            }, log);

            // Wire the director.
            var rm = GameObject.Find("Race_Manager");
            if (rm == null) return log + "ERROR: Race_Manager not found";
            var director = rm.GetComponent<SkyDirector>();
            if (director == null) director = Undo.AddComponent<SkyDirector>(rm);
            Undo.RecordObject(director, "sky director");
            director.skyMaterial = skyMat; director.defaultPreset = day; director.nightPreset = night;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) { director.sun = l; break; }
            director.volume = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            EditorUtility.SetDirty(director);

            // Events: e1 Day, e2 Morning, e3 Sunset, e4 Night, e5 Overcast.
            var map = new System.Collections.Generic.Dictionary<string, SkyPreset> { { "e1", day }, { "e2", morning }, { "e3", sunset }, { "e4", night }, { "e5", overcast } };
            foreach (var guid in AssetDatabase.FindAssets("t:RaceEventDefinition", new[] { "Assets/Data/Events" }))
            {
                var ev = AssetDatabase.LoadAssetAtPath<RaceEventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (ev != null && map.TryGetValue(ev.eventId, out var preset)) { ev.sky = preset; EditorUtility.SetDirty(ev); log.AppendLine("event " + ev.eventId + " -> " + preset.name); }
            }

            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("sceneSaved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
            return log.ToString();
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private static SkyPreset Preset(string name, System.Action<SkyPreset> fill, System.Text.StringBuilder log)
        {
            string path = $"{Dir}/{name}.asset";
            var p = AssetDatabase.LoadAssetAtPath<SkyPreset>(path);
            bool created = p == null;
            if (created) { p = ScriptableObject.CreateInstance<SkyPreset>(); p.name = name; }
            fill(p);
            if (created) AssetDatabase.CreateAsset(p, path); else EditorUtility.SetDirty(p);
            log.AppendLine((created ? "created " : "updated ") + path);
            return p;
        }

        private static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
