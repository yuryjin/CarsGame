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
        // Recorded ghosts from the previous raised merges no longer match this road.
        public const int LayoutVersion = 3;
        private const int Steps = 64;
        private readonly Vector3[] points = new Vector3[Steps + 1];
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<BoxCollider> openedBarriers = new List<BoxCollider>();
        private readonly Dictionary<MeshFilter, Mesh> originalSceneryMeshes = new Dictionary<MeshFilter, Mesh>();
        private readonly Dictionary<MeshCollider, Mesh> originalSceneryColliders = new Dictionary<MeshCollider, Mesh>();
        private TrackPath path;
        private float start, span;
        public float Length { get; private set; }
        public float RepairDistance { get; private set; }

        public static TrackFork Create(TrackPath path, bool hasRepair = true)
        {
            var fork = new GameObject("TrackFork").AddComponent<TrackFork>();
            fork.path = path;
            fork.Build(path.reversed ? path.Spline.Reversed() : path.Spline, hasRepair);
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

        private void Build(TrackSpline forward, bool hasRepair)
        {
            Vector3 a = forward.Position(Entry) + forward.Right(Entry) * 2.2f;
            Vector3 d = forward.Position(Exit) + forward.Right(Exit) * 2.2f;
            // Authored spline points are painted dashes, 1.5 cm above the road.
            // Anchor the physical deck to the road collider rather than that visual height.
            var surface = GameObject.Find("RaceTrack/Track_Road")?.GetComponent<Collider>();
            if (surface != null)
            {
                if (surface.Raycast(new Ray(a + Vector3.up, Vector3.down), out var entryHit, 2f)) a.y = entryHit.point.y;
                if (surface.Raycast(new Ray(d + Vector3.up, Vector3.down), out var exitHit, 2f)) d.y = exitHit.point.y;
            }
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
            }
            // A raised deck edge is itself an obstacle even without its visual skirt.
            // Keep both merges flush until the whole branch clears the main road,
            // then form the flyover between those points with zero grade at each end.
            int firstClear = -1, lastClear = -1;
            for (int i = 0; i <= Steps; i++)
            {
                hint = -1;
                float dMain = forward.Project(points[i], ref hint, 0);
                if (Mathf.Abs(forward.LateralOffset(points[i], dMain)) <= path.halfWidth + HalfWidth + 0.5f) continue;
                if (firstClear < 0) firstClear = i;
                lastClear = i;
            }
            for (int i = 0; i <= Steps; i++)
            {
                float t = lastClear > firstClear ? Mathf.Clamp01((i - firstClear) / (float)(lastClear - firstClear)) : 0f;
                float rise = Mathf.Sin(Mathf.PI * t);
                points[i].y += rise * rise * 6f;
                if (i > 0) Length += Vector3.Distance(points[i - 1], points[i]);
            }
            var road = new MeshChunkWriter("ShortcutBridge");
            var skirts = new MeshChunkWriter("ShortcutSkirts");
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
                    // These thin visual edges cross the main road at the merges.
                    // Colliding with them snags cars travelling along the long route;
                    // the driveable deck and structural supports remain solid.
                    skirts.AddQuad(edgeA, edgeB, edgeB + Vector3.down * 0.25f, edgeA + Vector3.down * 0.25f, asphalt);
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
                if (height <= 0.1f) continue;
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
            AddMeshes(road, material, true, 0.015f, surface);
            AddMeshes(skirts, material, false, 0.015f);
            AddMeshes(markings, material, false);
            AddMeshes(CircuitRoadside.ForkSigns(forward, path.halfWidth, hasRepair), material, false);
            AddMeshes(CircuitRoadside.WaterTower(forward), material, false);
            AddMeshes(CircuitRoadside.PitWalkway(forward), material, false);
            OpenMergeBarriers();
            foreach (string name in new[] { "Track_LeftKerb", "Track_RightKerb" })
            {
                var filter = GameObject.Find("RaceTrack/" + name)?.GetComponent<MeshFilter>();
                if (filter != null) CutScenery(filter, (a, b, c) => NearBranch(a) || NearBranch(b) || NearBranch(c));
            }
        }

        private bool NearBranch(Vector3 point)
        {
            Project(point, out float error);
            return error < (HalfWidth + 0.5f) * (HalfWidth + 0.5f);
        }

        private void OpenMergeBarriers()
        {
            var barriers = GameObject.Find("RaceTrack/Barriers")?.transform;
            if (barriers == null) return;
            Physics.SyncTransforms();
            for (int i = 0; i < Steps; i++)
            {
                Vector3 segment = points[i + 1] - points[i];
                Vector3 centre = (points[i] + points[i + 1]) * 0.5f + Vector3.up * 0.6f;
                foreach (var obstacle in Physics.OverlapBox(centre,
                    new Vector3(HalfWidth + 0.35f, 0.65f, segment.magnitude * 0.5f + 0.2f),
                    Quaternion.LookRotation(segment), 1 << barriers.gameObject.layer, QueryTriggerInteraction.Ignore))
                    if (obstacle is BoxCollider box && box.transform.IsChildOf(barriers) && !openedBarriers.Contains(box))
                        openedBarriers.Add(box);
            }
            if (openedBarriers.Count == 0) return;

            // The authored barriers share one coloured mesh. Cut the same boxes
            // from a private copy, preserving the baked colours and normals.
            foreach (var filter in barriers.GetComponentsInChildren<MeshFilter>())
                CutScenery(filter, (a, b, c) =>
                {
                    Vector3 centre = (a + b + c) / 3f;
                    foreach (var box in openedBarriers)
                        if (new Bounds(box.center, box.size + Vector3.one * 0.01f).Contains(box.transform.InverseTransformPoint(centre))) return true;
                    return false;
                });
            foreach (var box in openedBarriers) box.enabled = false;
        }

        private void CutScenery(MeshFilter filter, System.Func<Vector3, Vector3, Vector3, bool> remove)
        {
            var original = filter.sharedMesh;
            if (original == null) return;
            var vertices = original.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = filter.transform.TransformPoint(vertices[i]);
            var copy = Instantiate(original);
            copy.name = original.name + "_ForkOpenings";
            for (int submesh = 0; submesh < original.subMeshCount; submesh++)
            {
                var triangles = original.GetTriangles(submesh);
                var kept = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (remove(vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]])) continue;
                    kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
                }
                copy.SetTriangles(kept, submesh);
            }
            originalSceneryMeshes.Add(filter, original);
            meshes.Add(copy);
            filter.sharedMesh = copy;
            var collider = filter.GetComponent<MeshCollider>();
            if (collider != null)
            {
                originalSceneryColliders.Add(collider, collider.sharedMesh);
                collider.sharedMesh = copy;
            }
        }

        private Vector3 Right(int i) => Vector3.Cross(Vector3.up,
            points[Mathf.Min(Steps, i + 1)] - points[Mathf.Max(0, i - 1)]).normalized;

        // The main road already supports the flat merges. Overlapping collider
        // triangles can catch sphere wheels even when their heights agree.
        // Keep bridge collision only where the existing road has no surface.
        private Mesh OutsideMainRoad(Mesh source, Collider mainRoad)
        {
            var vertices = source.vertices;
            var triangles = source.triangles;
            var keptVertices = new List<Vector3>();
            var keptTriangles = new List<int>();
            var polygon = new List<Vector3>(4);
            bool Covered(Vector3 point) => mainRoad.Raycast(new Ray(point + Vector3.up * 20f, Vector3.down), out _, 40f);
            Vector3 Boundary(Vector3 inside, Vector3 outside)
            {
                for (int step = 0; step < 12; step++)
                {
                    Vector3 middle = (inside + outside) * 0.5f;
                    if (Covered(middle)) inside = middle; else outside = middle;
                }
                return (inside + outside) * 0.5f;
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                polygon.Clear();
                Vector3 previous = vertices[triangles[i + 2]];
                bool previousCovered = Covered(previous);
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3 current = vertices[triangles[i + corner]];
                    bool currentCovered = Covered(current);
                    if (currentCovered != previousCovered)
                        polygon.Add(currentCovered ? Boundary(current, previous) : Boundary(previous, current));
                    if (!currentCovered) polygon.Add(current);
                    previous = current; previousCovered = currentCovered;
                }
                int first = keptVertices.Count;
                keptVertices.AddRange(polygon);
                for (int corner = 1; corner + 1 < polygon.Count; corner++)
                {
                    keptTriangles.Add(first); keptTriangles.Add(first + corner); keptTriangles.Add(first + corner + 1);
                }
            }
            var mesh = new Mesh { name = source.name + "_Collision" };
            mesh.SetVertices(keptVertices);
            mesh.SetTriangles(keptTriangles, 0, true);
            return mesh;
        }

        private void AddMeshes(MeshChunkWriter writer, Material material, bool solid, float visualLift = 0f, Collider mainRoad = null)
        {
            foreach (var mesh in writer.Finish())
            {
                meshes.Add(mesh);
                Mesh collision = mesh;
                if (solid && mainRoad != null)
                {
                    collision = OutsideMainRoad(mesh, mainRoad);
                    meshes.Add(collision);
                }
                if (visualLift > 0f)
                {
                    if (solid && collision == mesh) { collision = Instantiate(mesh); meshes.Add(collision); }
                    var vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++) vertices[i].y += visualLift;
                    mesh.vertices = vertices;
                    mesh.RecalculateBounds();
                }
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.layer = LayerMask.NameToLayer("Track");
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                if (solid)
                {
                    go.AddComponent<MeshCollider>().sharedMesh = collision;
                    go.AddComponent<TireAudioSurface>().surface = TireSurface.Bridge;
                }
            }
        }

        public void Restore()
        {
            if (path != null && path.Fork == this) path.Fork = null;
            foreach (var pair in originalSceneryMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            originalSceneryMeshes.Clear();
            foreach (var pair in originalSceneryColliders) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            originalSceneryColliders.Clear();
            foreach (var box in openedBarriers) if (box != null) box.enabled = true;
            openedBarriers.Clear();
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
