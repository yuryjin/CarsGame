using System.Collections.Generic;
using System.Globalization;
using RacingGame.World.Gen;
using TMPro;
using UnityEngine;

namespace RacingGame
{
    /// <summary>Distance boards on the approach to the two turns demanding the most braking.</summary>
    public sealed class BrakingBoards : MonoBehaviour
    {
        private List<Mesh> meshes;

        internal static BrakingBoards Create(TrackPath path, Material material)
        {
            if (path == null || material == null) return null;
            var boards = new GameObject("BrakingBoards").AddComponent<BrakingBoards>();
            var writer = new MeshChunkWriter("RoadsideBoards");
            float previousApex = -1f;
            for (int turn = 0; turn < 2; turn++)
            {
                float apex = -1f, bestScore = 0f;
                for (float d = 0f; d < path.Length; d += 6f)
                {
                    if (previousApex >= 0f && Mathf.Abs(path.SignedDelta(previousApex, d)) < 180f) continue;
                    float drop = path.Profile.SpeedAt(d - 100f) - path.Profile.SpeedAt(d);
                    float severity = Mathf.Abs(path.Profile.CurvatureAt(d)) * drop;
                    if (drop > 3f && severity > bestScore) { bestScore = severity; apex = d; }
                }
                if (apex < 0f) break;
                float curvature = path.Profile.CurvatureAt(apex);
                float sign = Mathf.Sign(curvature), entry = apex;
                for (float lead = 4f; lead <= 64f; lead += 4f)
                {
                    if (path.Profile.CurvatureAt(apex - lead) * sign < Mathf.Abs(curvature) * .3f) break;
                    entry = apex - lead;
                }
                var corner = new GameObject("Turn").transform;
                corner.SetParent(boards.transform, false);
                corner.position = path.Position(entry);
                foreach (int distance in new[] { 100, 50, 25 })
                {
                    float d = entry - distance;
                    path.Pose(d, -sign * (path.halfWidth + 3.4f), out var foot, out var rotation);
                    var frame = Matrix4x4.TRS(foot, rotation, Vector3.one);
                    LowPoly.Box(writer, frame, Vector3.up * 1.45f, new Vector3(.15f, 2.9f, .15f), new Color32(98, 102, 101, 0));
                    LowPoly.Box(writer, frame, Vector3.up * 3f, new Vector3(3.8f, 2.1f, .14f), new Color32(41, 49, 53, 0));
                    var label = new GameObject(distance.ToString(CultureInfo.InvariantCulture)).AddComponent<TextMeshPro>();
                    label.transform.SetParent(corner, false);
                    label.transform.SetPositionAndRotation(frame.MultiplyPoint3x4(new Vector3(0f, 3f, -.09f)), rotation);
                    label.rectTransform.sizeDelta = new Vector2(3.8f, 2.1f);
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontSize = 16f;
                    label.fontStyle = FontStyles.Bold;
                    label.color = new Color(1f, .96f, .77f);
                    label.text = label.name;
                    label.raycastTarget = false;
                }
                previousApex = apex;
            }
            boards.meshes = writer.Finish();
            foreach (var mesh in boards.meshes)
            {
                var panel = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                panel.transform.SetParent(boards.transform, false);
                panel.GetComponent<MeshFilter>().sharedMesh = mesh;
                panel.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            return boards;
        }

        private void OnDestroy()
        {
            if (meshes != null) foreach (var mesh in meshes) Destroy(mesh);
        }
    }
}
