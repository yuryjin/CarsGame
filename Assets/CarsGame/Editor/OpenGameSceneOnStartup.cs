using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// When the Editor starts on an empty Untitled scene (last session closed without a saved
    /// scene), opens the first enabled build scene so pressing Play starts the game.
    /// Runs once per Editor session, never in batch mode, and leaves any real scene alone.
    /// </summary>
    [InitializeOnLoad]
    internal static class OpenGameSceneOnStartup
    {
        private const string DoneKey = "RacingGame.OpenGameSceneOnStartup.Done";

        static OpenGameSceneOnStartup()
        {
            if (UnityEngine.Application.isBatchMode || SessionState.GetBool(DoneKey, false)) return;
            SessionState.SetBool(DoneKey, true);
            EditorApplication.delayCall += OpenIfUntitled;
        }

        private static void OpenIfUntitled()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1) return;
            var active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty) return;

            foreach (var s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                return;
            }
        }
    }
}
