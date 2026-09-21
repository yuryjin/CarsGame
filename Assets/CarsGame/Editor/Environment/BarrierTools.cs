using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RacingGame.World.Gen;
using RacingGame.Core.Track;

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

            string cleared = ClearGantry(barriers, spline);
            RebuildMesh(barriers);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            return $"moved {n} barriers to |lateral|={offset}, {cleared} sceneSaved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Racing/Environment/Clear Barriers Off Start Gantry")]
        public static void ClearGantryMenu()
        {
            var spline = Object.FindFirstObjectByType<RacingGame.TrackPath>().Spline;
            var barriers = GameObject.Find("RaceTrack").transform.Find("Barriers");
            string log = ClearGantry(barriers, spline);
            RebuildMesh(barriers);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log + " sceneSaved=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
        }

        /// <summary>
        /// The loop seam sits under the start gantry, where the inner-corner barriers collapse onto the left leg and cross each other.
        /// Each barrier touching a leg slides along the track to the nearest free spot; if there is none it is hidden.
        /// </summary>
        public static string ClearGantry(Transform barriers, TrackSpline spline)
        {
            var gantry = GameObject.Find("RaceTrack").transform.Find("StartFinish_Gantry");
            if (gantry == null) return "no gantry";
            var legs = new List<(Vector2 c, float r)>();
            foreach (var col in gantry.GetComponentsInChildren<Collider>(true))
            {
                var b = col.bounds;
                if (b.size.y > 4f && b.size.x < 2f && b.size.z < 2f) legs.Add((new Vector2(b.center.x, b.center.z), Mathf.Max(b.extents.x, b.extents.z)));
            }

            (Vector2 a, Vector2 b) Seg(Vector3 pos, Quaternion rot, Transform t)
            {
                Vector3 half = rot * Vector3.forward * (t.lossyScale.z * 0.5f);
                return (new Vector2(pos.x - half.x, pos.z - half.z), new Vector2(pos.x + half.x, pos.z + half.z));
            }
            bool HitsLeg((Vector2 a, Vector2 b) s, Transform t)
            {
                foreach (var l in legs) if (PointSeg(l.c, s.a, s.b) < l.r + t.lossyScale.x * 0.5f + 0.35f) return true;
                return false;
            }
            bool NearLeg(Vector3 p)
            {
                foreach (var l in legs) if (Vector2.Distance(l.c, new Vector2(p.x, p.z)) < 10f) return true;
                return false;
            }
            bool HitsAny((Vector2 a, Vector2 b) s, Transform self, List<Transform> others)
            {
                foreach (var o in others)
                    if (o != self && SegSeg(s.a, s.b, Seg(o.position, o.rotation, o).a, Seg(o.position, o.rotation, o).b) < 1.0f) return true;
                return false;
            }

            // Near the legs: anything touching a leg, crooked against the track or overlapping a neighbour is re-placed from scratch
            // (hidden ones too, so re-running is stable). Everything else stays put and blocks.
            var fixedOnes = new List<Transform>(); var near = new List<Transform>();
            foreach (Transform ch in barriers)
            {
                if (ch.GetComponent<BoxCollider>() == null) continue;
                if (NearLeg(ch.position)) near.Add(ch); else if (ch.gameObject.activeSelf) fixedOnes.Add(ch);
            }
            var todo = new List<Transform>();
            foreach (var ch in near)
            {
                int hint = -1; float d = spline.Project(ch.position, ref hint);
                float ang = Vector3.Angle(ch.forward, spline.Tangent(d)); bool crooked = ang > 20f && ang < 160f;
                var others = new List<Transform>(fixedOnes); foreach (var o in near) if (o.gameObject.activeSelf) others.Add(o);
                if (!ch.gameObject.activeSelf || crooked || HitsLeg(Seg(ch.position, ch.rotation, ch), ch) || HitsAny(Seg(ch.position, ch.rotation, ch), ch, others)) todo.Add(ch);
                else fixedOnes.Add(ch);
            }

            int moved = 0, hidden = 0;
            foreach (var ch in todo)
            {
                int hint = -1; float d0 = spline.Project(ch.position, ref hint);
                float side = ch.name.StartsWith("Barrier_L_") ? -1f : ch.name.StartsWith("Barrier_R_") ? 1f : Mathf.Sign(spline.LateralOffset(ch.position, d0));
                bool done = false;
                for (float step = 0f; step <= 10f && !done; step += 0.5f)
                    foreach (float dd in new[] { d0 + step, d0 - step })
                    {
                        Vector3 pos = spline.Position(dd) + spline.Right(dd) * (side * 5.8f) + Vector3.up * 0.55f;
                        var rot = Quaternion.LookRotation(spline.Tangent(dd), Vector3.up);
                        var s = Seg(pos, rot, ch);
                        int h = -1; float dp = spline.Project(pos, ref h);       // inside a hairpin the spot can belong to another stretch of track
                        if (Vector3.Angle(rot * Vector3.forward, spline.Tangent(dp)) > 20f) continue;
                        if (HitsLeg(s, ch) || HitsAny(s, ch, fixedOnes)) continue;
                        Undo.RecordObject(ch, "clear gantry"); ch.position = pos; ch.rotation = rot;
                        Undo.RecordObject(ch.gameObject, "clear gantry"); ch.gameObject.SetActive(true);
                        fixedOnes.Add(ch); moved++; done = true; break;
                    }
                if (!done) { Undo.RecordObject(ch.gameObject, "clear gantry"); ch.gameObject.SetActive(false); hidden++; }
            }
            return $"gantry legs={legs.Count}: barriers moved={moved} hidden={hidden}";
        }

        /// <summary>Rebuild the combined red/white mesh from the active barrier colliders.</summary>
        public static void RebuildMesh(Transform barriers)
        {
            var old = barriers.Find("Barriers_Mesh"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var litMat = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentBaker.MatDir + "/M_Env_Lit.mat");
            var w = new MeshChunkWriter("Barriers");
            var white = MeshChunkWriter.Srgb(0.90f, 0.91f, 0.93f); var red = MeshChunkWriter.Srgb(0.86f, 0.14f, 0.14f);

            var leftList = new List<Transform>();
            var rightList = new List<Transform>();
            foreach (Transform ch in barriers)
            {
                if (ch.GetComponent<BoxCollider>() == null || !ch.gameObject.activeSelf) continue;
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
        }

        private static float PointSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        private static float SegSeg(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            float d1 = Cross(b - a, c - a), d2 = Cross(b - a, d - a), d3 = Cross(d - c, a - c), d4 = Cross(d - c, b - c);
            if (d1 * d2 < 0f && d3 * d4 < 0f) return 0f;
            return Mathf.Min(Mathf.Min(PointSeg(a, c, d), PointSeg(b, c, d)), Mathf.Min(PointSeg(c, a, b), PointSeg(d, a, b)));
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
