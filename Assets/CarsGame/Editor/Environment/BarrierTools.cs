using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame.EditorTools
{
    /// <summary>Barrier placement relative to the spline (the authored ones sit right on the AI's swept path).</summary>
    public static class BarrierTools
    {
        [MenuItem("Racing/Environment/Measure Barriers")]
        public static void MeasureMenu() { Debug.Log(Measure()); }

        [MenuItem("Racing/Environment/Re-seat Barriers (5.8m)")]
        public static void MoveMenu() { Debug.Log(Move(5.8f)); }

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

                float side = 1f;
                int k = -1;
                string[] parts = ch.name.Split('_');
                if (parts.Length == 3 && int.TryParse(parts[2], out k))
                {
                    side = parts[1] == "L" ? -1f : 1f;
                }
                else
                {
                    int h = -1;
                    float fallbackD = spline.Project(ch.position, ref h);
                    side = Mathf.Sign(spline.LateralOffset(ch.position, fallbackD));
                    if (side == 0f) side = 1f;
                }

                float d;
                if (k >= 0)
                {
                    int seg = (k / 3) % spline.PointCount;
                    float t = (k % 3) / 3f;
                    Vector3 pA = spline.Point(seg);
                    Vector3 pB = spline.Point(seg + 1);
                    Vector3 posOnSpline = Vector3.Lerp(pA, pB, t);
                    int hint = seg;
                    d = spline.Project(posOnSpline, ref hint, 2);
                }
                else
                {
                    int hint = -1;
                    d = spline.Project(ch.position, ref hint);
                }

                Undo.RecordObject(ch, "move barrier");
                ch.position = spline.Position(d) + spline.Right(d) * (side * offset) + Vector3.up * 0.55f;
                ch.rotation = Quaternion.LookRotation(spline.Tangent(d), Vector3.up);
                n++;
            }

            var old = barriers.Find("Barriers_Mesh"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var litMat = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentBaker.MatDir + "/M_Env_Lit.mat");
            var w = new MeshChunkWriter("Barriers");
            var white = MeshChunkWriter.Srgb(0.90f, 0.91f, 0.93f); var red = MeshChunkWriter.Srgb(0.86f, 0.14f, 0.14f);

            var leftList = new List<Transform>();
            var rightList = new List<Transform>();
            foreach (Transform ch in barriers)
            {
                if (ch.GetComponent<BoxCollider>() == null) continue;
                if (ch.name.StartsWith("Barrier_L_")) leftList.Add(ch);
                else rightList.Add(ch);
            }
            int ParseIndex(Transform t)
            {
                var p = t.name.Split('_');
                return (p.Length == 3 && int.TryParse(p[2], out int idx)) ? idx : 0;
            }
            leftList.Sort((a, b) => ParseIndex(a).CompareTo(ParseIndex(b)));
            rightList.Sort((a, b) => ParseIndex(a).CompareTo(ParseIndex(b)));

            int i = 0;
            foreach (var ch in leftList)
            {
                var bc = ch.GetComponent<BoxCollider>();
                LowPoly.Box(w, ch.localToWorldMatrix, bc.center, bc.size, (i++ % 2 == 0) ? white : red);
            }
            i = 0;
            foreach (var ch in rightList)
            {
                var bc = ch.GetComponent<BoxCollider>();
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
