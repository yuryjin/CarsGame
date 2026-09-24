using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Track;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>One narrow flyover inside the mountain circuit's north bend.</summary>
    public sealed class TrackFork : MonoBehaviour
    {
        // ponytail: one authored mountain bend; use per-track endpoints when another ground circuit is added.
        public const float Entry = 85f, Exit = 265f, HalfWidth = 2.2f;
        private const int Steps = 64;
        private readonly Vector3[] points = new Vector3[Steps + 1];
        private readonly List<Mesh> meshes = new List<Mesh>();
        private TrackPath path;
        private float start, span;
        public float Length { get; private set; }
        public float RepairDistance { get; private set; }

        public static TrackFork Create(TrackPath path)
        {
            var fork = new GameObject("TrackFork").AddComponent<TrackFork>();
            fork.path = path;
            fork.Build(path.reversed ? path.Spline.Reversed() : path.Spline);
            path.Fork = fork;
            return fork;
        }

        public Vector3 Position(float fraction)
        {
            float scaled = Mathf.Clamp01(fraction) * Steps;
            int i = Mathf.Min((int)scaled, Steps - 1);
            return Vector3.Lerp(points[i], points[i + 1], scaled - i);
        }

        /// <summary>Map the physical branch onto the same ordered lap gates as the long road.</summary>
        public float Project(Vector3 position, out float squaredError)
        {
            squaredError = float.MaxValue;
            float fraction = 0f;
            for (int i = 0; i < Steps; i++)
            {
                Vector3 segment = points[i + 1] - points[i];
                float t = Mathf.Clamp01(Vector3.Dot(position - points[i], segment) / segment.sqrMagnitude);
                float error = (position - Vector3.Lerp(points[i], points[i + 1], t)).sqrMagnitude;
                if (error >= squaredError) continue;
                squaredError = error;
                fraction = (i + t) / Steps;
            }
            return path.Wrap(start + span * fraction);
        }

        private void Build(TrackSpline forward)
        {
            Vector3 a = forward.Position(Entry) + forward.Right(Entry) * 2.2f;
            Vector3 d = forward.Position(Exit) + forward.Right(Exit) * 2.2f;
            Vector3 b = a + forward.Tangent(Entry) * 50f;
            Vector3 c = d - forward.Tangent(Exit) * 50f;
            int hint = -1;
            start = path.Spline.Project(a, ref hint, 0);
            hint = -1;
            span = path.SignedDelta(start, path.Spline.Project(d, ref hint, 0));
            hint = -1;
            RepairDistance = path.Spline.Project(forward.Position((Entry + Exit) * 0.5f), ref hint, 0);
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps, u = 1f - t;
                points[i] = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
                points[i].y += Mathf.Sin(Mathf.PI * t) * Mathf.Sin(Mathf.PI * t) * 6f + 0.015f;
                if (i > 0) Length += Vector3.Distance(points[i - 1], points[i]);
            }
            var road = new MeshChunkWriter("ShortcutBridge");
            var markings = new MeshChunkWriter("ShortcutMarkings");
            var asphalt = new Color32(64, 70, 78, 0);
            for (int i = 0; i < Steps; i++)
            {
                Vector3 rightA = Right(i), rightB = Right(i + 1);
                Vector3 p = points[i], q = points[i + 1];
                road.AddQuad(p - rightA * HalfWidth, q - rightB * HalfWidth,
                    q + rightB * HalfWidth, p + rightA * HalfWidth, asphalt);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 edgeA = p + rightA * (side * HalfWidth), edgeB = q + rightB * (side * HalfWidth);
                    road.AddQuad(edgeA, edgeB, edgeB + Vector3.down * 0.25f, edgeA + Vector3.down * 0.25f, asphalt);
                    var color = i % 4 < 2 ? new Color32(255, 180, 35, 0) : new Color32(30, 33, 40, 0);
                    Vector3 up = Vector3.up * 0.025f;
                    float left = side < 0f ? -HalfWidth : HalfWidth - 0.25f;
                    float right = left + 0.25f;
                    markings.AddQuad(p + rightA * left + up, q + rightB * left + up,
                        q + rightB * right + up, p + rightA * right + up, color);
                }
            }
            for (int i = 16; i <= 48; i += 16)
            {
                Vector3 foot = points[i];
                float height = foot.y - 0.25f;
                foot.y = height * 0.5f;
                LowPoly.Box(road, Matrix4x4.identity, foot, new Vector3(1f, height, 1f), new Color32(100, 105, 112, 0));
            }
            // Arrows at both ends make the branch readable in forward and reverse races.
            foreach (int index in new[] { 3, Steps - 3 })
            {
                Vector3 direction = (points[index + 1] - points[index - 1]).normalized * (index < Steps / 2 ? 1f : -1f);
                Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 p = points[index] + Vector3.up * 0.04f;
                markings.AddTriangle(p - right, p + direction * 2.5f, p + right, new Color32(255, 195, 50, 0));
            }
            var terrain = GameObject.Find("Environment/Terrain");
            var renderer = terrain != null ? terrain.GetComponentInChildren<Renderer>() : null;
            var material = renderer != null ? renderer.sharedMaterial : null;
            AddMeshes(road, material, true);
            AddMeshes(markings, material, false);
        }

        private Vector3 Right(int i) => Vector3.Cross(Vector3.up,
            points[Mathf.Min(Steps, i + 1)] - points[Mathf.Max(0, i - 1)]).normalized;

        private void AddMeshes(MeshChunkWriter writer, Material material, bool solid)
        {
            foreach (var mesh in writer.Finish())
            {
                meshes.Add(mesh);
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.layer = LayerMask.NameToLayer("Track");
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        public void Restore()
        {
            if (path != null && path.Fork == this) path.Fork = null;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Restore();
            foreach (var mesh in meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
