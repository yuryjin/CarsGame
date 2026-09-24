using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>Space dressing and boost gates over the existing suspended circuit.</summary>
    public sealed class SpaceTrack : MonoBehaviour
    {
        private GameObject mountains;
        private Material glow;
        private readonly List<Mesh> meshes = new List<Mesh>();

        public static SpaceTrack Create(TrackPath path, AbyssTrack road)
        {
            var map = new GameObject("SpaceTrack").AddComponent<SpaceTrack>();
            road.SetLap(2); // Constant full-width road without barriers; no lap subscription.
            map.mountains = GameObject.Find("Environment/Mountains");
            if (map.mountains != null) map.mountains.SetActive(false);
            map.glow = new Material(road.GetComponentInChildren<Renderer>().sharedMaterial) { name = "SpaceGlow" };
            map.glow.SetFloat("_EmissionBoost", 3f);
            map.Build(path);
            return map;
        }

        private void Build(TrackPath path)
        {
            var rings = new MeshChunkWriter("GateRings");
            for (int i = 0; i < 6; i++)
            {
                float distance = path.startLineDistance + 80f + i * (path.Length - 80f) / 6f;
                path.Pose(distance, 0f, out var position, out var rotation);
                var gate = new GameObject("BoostGate_" + (i + 1), typeof(BoostGate), typeof(BoxCollider));
                gate.transform.SetParent(transform, false);
                gate.transform.SetPositionAndRotation(position + Vector3.up * 3f, rotation);
                var trigger = gate.GetComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(BoostGate.Radius * 2f, BoostGate.Radius * 2f, 3f);
                var color = i % 2 == 0 ? new Color32(60, 205, 255, 255) : new Color32(195, 90, 255, 255);
                // A closed tube around an open circular aperture, facing the direction of travel.
                for (int segment = 0; segment < 64; segment++)
                    for (int tube = 0; tube < 8; tube++)
                    {
                        Vector3 Point(int s, int t)
                        {
                            float a = s * Mathf.PI * 2f / 64f, b = t * Mathf.PI * 2f / 8f;
                            float radius = BoostGate.Radius + 0.3f + Mathf.Cos(b) * 0.3f;
                            return gate.transform.TransformPoint(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, Mathf.Sin(b) * 0.3f));
                        }
                        rings.AddQuad(Point(segment, tube), Point(segment + 1, tube), Point(segment + 1, tube + 1), Point(segment, tube + 1), color);
                    }
            }
            AddMeshes(rings);
            var lights = new MeshChunkWriter("SpaceLights");
            int count = Mathf.CeilToInt(path.Length / 3f);
            for (int i = 0; i < count; i++)
                foreach (float side in new[] { -1f, 1f })
                {
                    float a = i * path.Length / count, b = (i + 1) * path.Length / count;
                    Vector3 p = path.Position(a) + path.Right(a) * (side * 4.85f) + Vector3.up * 0.03f;
                    Vector3 q = path.Position(b) + path.Right(b) * (side * 4.85f) + Vector3.up * 0.03f;
                    lights.AddQuad(p - path.Right(a) * 0.08f, q - path.Right(b) * 0.08f,
                        q + path.Right(b) * 0.08f, p + path.Right(a) * 0.08f, new Color32(45, 175, 255, 255));
                }
            // Stars below and around the road make the void readable from the chase camera too.
            var random = new System.Random(1701);
            for (int i = 0; i < 240; i++)
            {
                float y = (float)random.NextDouble() * 2f - 1f;
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float horizontal = Mathf.Sqrt(1f - y * y);
                var position = new Vector3(Mathf.Cos(angle) * horizontal, y, Mathf.Sin(angle) * horizontal) * 400f;
                LowPoly.Box(lights, Matrix4x4.identity, position, Vector3.one * 0.7f, new Color32(170, 195, 255, 255));
            }
            AddMeshes(lights);
        }

        private void AddMeshes(MeshChunkWriter writer)
        {
            foreach (var mesh in writer.Finish())
            {
                meshes.Add(mesh);
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = glow;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public void Restore()
        {
            if (mountains != null) { mountains.SetActive(true); mountains = null; }
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Restore();
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            if (glow != null) Destroy(glow);
        }
    }
}
