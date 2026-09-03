using UnityEditor;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame.EditorTools
{
    /// <summary>Barrier placement relative to the spline (the authored ones sit right on the AI's swept path).</summary>
    public static class BarrierTools
    {
        public static string Measure()
        {
            var track = Object.FindFirstObjectByType<RacingGame.TrackPath>(); var spline = track.Spline;
            var barriers = GameObject.Find("RaceTrack").transform.Find("Barriers");
            float min = 99f, max = 0f, sum = 0f; int n = 0;
            foreach (Transform ch in barriers)
            {
                if (ch.GetComponent<BoxCollider>() == null) continue;
                int hint = -1; float d = spline.Project(ch.position, ref hint);
                float lat = Mathf.Abs(spline.LateralOffset(ch.position, d));
                min = Mathf.Min(min, lat); max = Mathf.Max(max, lat); sum += lat; n++;
            }
            return $"barriers={n} lateral min={min:F2} max={max:F2} avg={sum / Mathf.Max(1, n):F2} halfWidth={track.halfWidth}";
        }

        /// <summary>Re-seat every barrier at |lateral| = offset on its own side, aligned with the tangent, and rebuild the combined mesh.</summary>
        public static string Move(float offset)
        {
            var track = Object.FindFirstObjectByType<RacingGame.TrackPath>(); var spline = track.Spline;
            var barriers = GameObject.Find("RaceTrack").transform.Find("Barriers");
            int n = 0;
            foreach (Transform ch in barriers)
            {
                if (ch.GetComponent<BoxCollider>() == null) continue;
                int hint = -1; float d = spline.Project(ch.position, ref hint);
                float side = Mathf.Sign(spline.LateralOffset(ch.position, d));
                if (side == 0f) side = 1f;
                Undo.RecordObject(ch, "move barrier");
                ch.position = spline.Position(d) + spline.Right(d) * (side * offset) + Vector3.up * ch.position.y;
                ch.rotation = Quaternion.LookRotation(spline.Tangent(d), Vector3.up);
                n++;
            }
            var old = barriers.Find("Barriers_Mesh"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var litMat = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentBaker.MatDir + "/M_Env_Lit.mat");
            var w = new MeshChunkWriter("Barriers"); int i = 0;
            var white = MeshChunkWriter.Srgb(0.90f, 0.91f, 0.93f); var red = MeshChunkWriter.Srgb(0.86f, 0.14f, 0.14f);
            foreach (Transform ch in barriers)
            {
                var bc = ch.GetComponent<BoxCollider>(); if (bc == null) continue;
                LowPoly.Box(w, ch.localToWorldMatrix, bc.center, bc.size, (i++ % 2 == 0) ? white : red);
            }
            foreach (var mesh in w.Finish()) { var go = EnvironmentBaker.Place(barriers, EnvironmentBaker.SaveMesh(mesh, "Barriers_0"), litMat, true, true, barriers.gameObject.layer); go.name = "Barriers_Mesh"; }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            return $"moved {n} barriers to |lateral|={offset} sceneSaved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        public static string SetColliders(bool enabled)
        {
            var barriers = GameObject.Find("RaceTrack").transform.Find("Barriers"); int n = 0;
            foreach (var bc in barriers.GetComponentsInChildren<BoxCollider>(true)) { Undo.RecordObject(bc, "col"); bc.enabled = enabled; n++; }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            return $"barrier colliders enabled={enabled} ({n}) saved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }
}
