using System.Collections.Generic;
using RacingGame.World.Gen;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingGame
{
    public sealed class CornerReflectors : MonoBehaviour
    {
        private List<Mesh> chunks;
        private Material reflect;
        public int Markers { get; private set; }

        public void Build(TrackPath path, Material scenery)
        {
            if (scenery == null) return;
            reflect = new Material(scenery) { name = "Night road reflectors" };
            reflect.SetFloat("_EmissionBoost", 1.9f);
            reflect.SetFloat("_EmissionByLamps", 0f);
            var writer = new MeshChunkWriter("OuterEdgeReflectors");
            foreach (var corner in PropLayout.SharpestCorners(path.Spline, 3, 82f))
                for (int i = -2; i <= 4; i++)
                {
                    float d = corner.d + i * 8f;
                    path.Pose(d, -corner.sign * (path.halfWidth + .85f), out var foot, out var rot);
                    var frame = Matrix4x4.TRS(foot, rot, Vector3.one);
                    LowPoly.Box(writer, frame, new Vector3(0, .76f, 0), new Vector3(.18f, 1.52f, .18f), new Color32(128, 126, 115, 0));
                    LowPoly.Box(writer, frame, new Vector3(0, 1.52f, 0), new Vector3(.49f, .49f, .19f), new Color32(36, 35, 34, 0));
                    LowPoly.Box(writer, frame, new Vector3(0, 1.56f, 0), new Vector3(.39f, .23f, .23f), new Color32(255, 189, 77, 250));
                    LowPoly.Box(writer, frame, new Vector3(0, 1.36f, 0), new Vector3(.25f, .065f, .23f), new Color32(250, 246, 219, 235));
                    Markers++;
                }
            chunks = writer.Finish();
            foreach (var mesh in chunks)
            {
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = reflect;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private void OnDestroy()
        {
            if (chunks != null) foreach (var mesh in chunks) Destroy(mesh);
            if (reflect != null) Destroy(reflect);
        }
    }
}
