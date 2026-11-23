using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Track;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>One narrow flyover inside the mountain circuit's north bend.</summary>
    public sealed partial class TrackFork : MonoBehaviour
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
        public bool HasRepair { get; private set; }

        public MountainFork Course { get; private set; }
        public bool IsDirt => Course == MountainFork.Quarry;
        public float RoadHalfWidth => IsDirt ? 4.3f : Course == MountainFork.Rooftops ? 4.8f : HalfWidth;
        public float ApproachDistance => path.reversed ? path.Wrap(start + span) : start;
        public float MainSpan => Mathf.Abs(span);

        public static TrackFork Create(TrackPath path, bool hasRepair = true, MountainFork course = MountainFork.Bridge)
        {
            var fork = new GameObject("TrackFork").AddComponent<TrackFork>();
            fork.path = path;
            fork.Course = course;
            fork.HasRepair = hasRepair;
            fork.Build(path.reversed ? path.Spline.Reversed() : path.Spline, hasRepair);
            path.Fork = fork;
            return fork;
        }

        /// <summary>Drive the narrow route from the approach through its merge in either direction.</summary>
        public void FollowShortcut(CarController car, float distance, out float steer, out float throttle)
        {
            float progress = path.SignedDelta(ApproachDistance, distance) / MainSpan;
            if (progress >= 0f && progress <= 1f)
            {
                Project(car.transform.position, out _, out float fraction);
                progress = path.reversed ? 1f - fraction : fraction;
            }
            const float step = .015f;
            Vector3 a = DrivingLine(progress - step), centre = DrivingLine(progress), b = DrivingLine(progress + step);
            Vector3 incoming = Vector3.ProjectOnPlane(centre - a, Vector3.up);
            Vector3 outgoing = Vector3.ProjectOnPlane(b - centre, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, incoming + outgoing).normalized;
            float curvature = Vector3.SignedAngle(incoming, outgoing, Vector3.up) * Mathf.Deg2Rad /
                Mathf.Max(.1f, (incoming + outgoing).magnitude * .5f);
            Vector3 target = DrivingLine(progress + .05f) - car.transform.position;
            target.y = 0f;
            float speed = car.Body.linearVelocity.magnitude;
            float facing = Vector3.SignedAngle(car.transform.forward, target, Vector3.up) - target.magnitude * curvature * .5f * Mathf.Rad2Deg;
            float correction = IsDirt ? .28f : .6f;
            float damping = IsDirt ? .11f : .15f;
            float lateral = Vector3.Dot(centre - car.transform.position, right) * correction
                - Vector3.Dot(car.Body.linearVelocity, right) * damping;
            steer = Mathf.Clamp(car.SteerForCurvature(curvature, speed) + lateral + facing / 35f
                - (car.Body.angularVelocity.y - speed * curvature) * .7f, -1f, 1f);
            if (Course == MountainFork.Rooftops && !path.reversed && progress >= .30f && progress <= .64f)
                steer = AimForLanding(car, progress, speed);
            float desired = Mathf.Min(IsDirt ? 76f : 45f, car.MaxSpeedForCurvature(curvature) * 3.6f * (IsDirt ? .78f : .82f));
            if (progress < .07f) desired = Mathf.Min(desired, 36f);
            if (IsDirt && progress > .87f) desired = Mathf.Min(desired, 45f);
            throttle = Mathf.Clamp((desired - car.SpeedKmh) / 3f, -1f, 1f);
        }

        private Vector3 DrivingLine(float t)
        {
            if (t >= 0f && t <= 1f) return Position(path.reversed ? 1f - t : t);
            float d = ApproachDistance + t * MainSpan;
            float lane = t <= 1f ? 2.2f : 2.2f * Mathf.Clamp01(1f - (t - 1f) * MainSpan / 16f);
            return path.Position(d) + path.Right(d) * (path.reversed ? -lane : lane);
        }

        public Vector3 Position(float fraction)
        {
            float scaled = Mathf.Clamp01(fraction) * Steps;
            int i = Mathf.Min((int)scaled, Steps - 1);
            return Vector3.Lerp(points[i], points[i + 1], scaled - i);
        }

        /// <summary>Map the physical branch onto the same ordered lap gates as the long road.</summary>
        public float Project(Vector3 position, out float squaredError) => Project(position, out squaredError, out _);

        internal float Project(Vector3 position, out float squaredError, out float fraction)
        {
            squaredError = float.MaxValue;
            fraction = 0f;
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
            float exit = IsDirt ? 310f : Exit;
            Vector3 a = forward.Position(Entry) + forward.Right(Entry) * 2.2f;
            Vector3 d = forward.Position(exit) + forward.Right(exit) * 2.2f;
            // Authored spline points are painted dashes, 1.5 cm above the road.
            // Anchor the physical deck to the road collider rather than that visual height.
            var surface = GameObject.Find("RaceTrack/Track_Road")?.GetComponent<Collider>();
            if (surface != null)
            {
                if (surface.Raycast(new Ray(a + Vector3.up, Vector3.down), out var entryHit, 2f)) a.y = entryHit.point.y;
                if (surface.Raycast(new Ray(d + Vector3.up, Vector3.down), out var exitHit, 2f)) d.y = exitHit.point.y;
            }
            Vector3 b = a + forward.Tangent(Entry) * (IsDirt ? 40f : 50f);
            Vector3 c = d - forward.Tangent(exit) * (IsDirt ? 32f : 50f);
            int hint = -1;
            start = path.Spline.Project(a, ref hint, 0);
            hint = -1;
            span = path.SignedDelta(start, path.Spline.Project(d, ref hint, 0));
            hint = -1;
            RepairDistance = path.Spline.Project(forward.Position((Entry + exit) * 0.5f), ref hint, 0);
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
                if (Mathf.Abs(forward.LateralOffset(points[i], dMain)) <= path.halfWidth + RoadHalfWidth + 0.5f) continue;
                if (firstClear < 0) firstClear = i;
                lastClear = i;
            }
            for (int i = 0; i <= Steps; i++)
            {
                float t = lastClear > firstClear ? Mathf.Clamp01((i - firstClear) / (float)(lastClear - firstClear)) : 0f;
                float rise = Mathf.Sin(Mathf.PI * t);
                points[i].y += rise * rise * (IsDirt ? .45f : 6f);
                if (Course == MountainFork.Rooftops && i >= RoofRampStart && i <= RoofLip)
                    points[i].y += (i - RoofRampStart) * .26f;
                if (Course == MountainFork.Rooftops && i >= RoofLanding && i <= RoofLanding + 14)
                    points[i].y += 1.25f * Mathf.Clamp01((RoofLanding + 14 - i) / 10f);
                if (i > 0) Length += Vector3.Distance(points[i - 1], points[i]);
            }
            var road = new MeshChunkWriter(IsDirt ? "DirtShortcut" : "ShortcutBridge");
            var skirts = new MeshChunkWriter("ShortcutSkirts");
            var markings = new MeshChunkWriter("ShortcutMarkings");
            var asphalt = IsDirt ? new Color32(158, 105, 60, 0) : new Color32(64, 70, 78, 0);
            for (int i = 0; i < Steps; i++)
            {
                if (Course == MountainFork.Rooftops && i >= RoofLip && i < RoofLanding)
                    continue;
                Vector3 rightA = Right(i), rightB = Right(i + 1);
                Vector3 p = points[i], q = points[i + 1];
                Color32 deckColor = Course == MountainFork.Rooftops && i >= RoofLanding && i < RoofLanding + 4
                    ? new Color32(218, 160, 45, 0) : asphalt;
                road.AddQuad(p - rightA * RoadHalfWidth, q - rightB * RoadHalfWidth,
                    q + rightB * RoadHalfWidth, p + rightA * RoadHalfWidth, deckColor);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 edgeA = p + rightA * (side * RoadHalfWidth), edgeB = q + rightB * (side * RoadHalfWidth);
                    // These thin visual edges cross the main road at the merges.
                    // Colliding with them snags cars travelling along the long route;
                    // the driveable deck and structural supports remain solid.
                    if (IsDirt)
                    {
                        Vector3 footA = edgeA + rightA * (side * 1.4f), footB = edgeB + rightB * (side * 1.4f);
                        footA.y = Mathf.Min(edgeA.y - .25f, 0f); footB.y = Mathf.Min(edgeB.y - .25f, 0f);
                        skirts.AddQuad(edgeA, edgeB, footB, footA, new Color32(112, 75, 43, 0));
                        Vector3 rutA = p + rightA * (side * .7f) + Vector3.up * .02f;
                        Vector3 rutB = q + rightB * (side * .7f) + Vector3.up * .02f;
                        markings.AddQuad(rutA, rutB, rutB + rightB * .2f, rutA + rightA * .2f, new Color32(125, 81, 48, 0));
                        continue;
                    }
                    skirts.AddQuad(edgeA, edgeB, edgeB + Vector3.down * 0.25f, edgeA + Vector3.down * 0.25f, asphalt);
                    var color = i % 4 < 2 ? new Color32(255, 180, 35, 0) : new Color32(30, 33, 40, 0);
                    Vector3 up = Vector3.up * 0.025f;
                    float left = side < 0f ? -RoadHalfWidth : RoadHalfWidth - 0.25f;
                    float right = left + 0.25f;
                    markings.AddQuad(p + rightA * left + up, q + rightB * left + up,
                        q + rightB * right + up, p + rightA * right + up, color);
                }
            }
            for (int i = 16; !IsDirt && Course != MountainFork.Rooftops && i <= 48; i += 16)
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
            AddMeshes(CircuitRoadside.ForkSigns(forward, path.halfWidth, hasRepair, exit), material, false);
            AddMeshes(CircuitRoadside.WaterTower(forward), material, false);
            AddMeshes(CircuitRoadside.PitWalkway(forward), material, false);
            if (Course == MountainFork.Rooftops)
            {
                BuildRooftops(material);
                path.RespawnGuard = RoofRespawn;
            }
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
            return error < (RoadHalfWidth + 0.5f) * (RoadHalfWidth + 0.5f);
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
                    new Vector3(RoadHalfWidth + 0.35f, 0.65f, segment.magnitude * 0.5f + 0.2f),
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
                go.layer = LayerMask.NameToLayer(solid && IsDirt ? "Ground" : "Track");
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
            if (Course == MountainFork.Rooftops && path != null && path.RespawnGuard == RoofRespawn)
                path.RespawnGuard = null;
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
